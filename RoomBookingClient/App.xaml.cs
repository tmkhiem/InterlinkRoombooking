using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace RoomBookingClient;

public partial class App : Application
{
    private readonly SettingsStore _settingsStore = new();
    private readonly MeetingSyncService _meetingSyncService = new();
    private readonly DispatcherTimer _windowVisibilityTimer;
    private readonly DispatcherTimer _meetingWarningTimer;
    private readonly SingleInstanceManager _singleInstanceManager;

    private NotifyIcon? _trayIcon;
    private ConfigWindow? _configWindow;
    private MeetingDetailsWindow? _meetingDetailsWindow;
    private OvertimeWarningWindow? _warningWindow;

    private ClientSettings _settings = ClientSettings.Default();
    private MeetingSnapshot? _activeMeeting;
    private int? _warningShownForBookingId;

    public App()
    {
        _singleInstanceManager = new SingleInstanceManager("InterlinkRoomBookingClient", "InterlinkRoomBookingClientSignal");

        _windowVisibilityTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _windowVisibilityTimer.Tick += (_, _) => UpdateMeetingWindowVisibility();

        _meetingWarningTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(20)
        };
        _meetingWarningTimer.Tick += (_, _) => CheckMeetingOvertime();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        TempMeetingStorage.DeleteRoot();

        if (!_singleInstanceManager.TryStart(() => Dispatcher.Invoke(ShowDashboard)))
        {
            _singleInstanceManager.SignalRunningInstance();
            Shutdown();
            return;
        }

        _settings = _settingsStore.Load();

        _configWindow = new ConfigWindow(_settings)
        {
            Visibility = Visibility.Hidden,
            ShowInTaskbar = false,
            WindowState = WindowState.Minimized
        };

        _configWindow.SaveRequested += OnConfigSaveRequested;
        _configWindow.RefreshRequested += (_, _) => _meetingSyncService.TriggerRefresh();
        _configWindow.TestWarningRequested += (_, _) => ShowWarning(true);

        _meetingDetailsWindow = new MeetingDetailsWindow();
        _meetingDetailsWindow.Hide();

        InitializeTrayIcon();

        _meetingSyncService.MeetingUpdated += OnMeetingUpdated;
        _meetingSyncService.ErrorOccurred += OnMeetingError;
        _meetingSyncService.Start(() => _settings);

        _windowVisibilityTimer.Start();
        _meetingWarningTimer.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _meetingWarningTimer.Stop();
        _windowVisibilityTimer.Stop();
        _meetingSyncService.Stop();

        _trayIcon?.Dispose();
        _singleInstanceManager.Dispose();

        TempMeetingStorage.CleanupRoot();

        base.OnExit(e);
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "Interlink Room Booking Client"
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open dashboard", null, (_, _) => ShowDashboard());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowDashboard();
    }

    private void OnConfigSaveRequested(object? sender, ClientSettings settings)
    {
        _settings = settings;
        _settingsStore.Save(settings);
        _meetingSyncService.TriggerRefresh();
    }

    private void OnMeetingUpdated(object? sender, MeetingSnapshot? snapshot)
    {
        Dispatcher.Invoke(() =>
        {
            _activeMeeting = snapshot;
            _meetingDetailsWindow?.SetMeeting(snapshot);

            if (snapshot == null)
            {
                _warningShownForBookingId = null;
                _warningWindow?.Close();
                _warningWindow = null;
            }

            UpdateMeetingWindowVisibility();
        });
    }

    private void OnMeetingError(object? sender, string message)
    {
        Dispatcher.Invoke(() => _configWindow?.SetStatus(message));
    }

    private void CheckMeetingOvertime()
    {
        if (_activeMeeting == null)
        {
            return;
        }

        if (DateTime.Now <= _activeMeeting.EndLocal)
        {
            return;
        }

        if (_warningShownForBookingId == _activeMeeting.BookingId)
        {
            return;
        }

        ShowWarning(false);
    }

    private void ShowWarning(bool isTest)
    {
        var message = isTest
            ? "Test warning: meeting overtime notification."
            : "Current meeting has exceeded its allotted time.";

        _warningWindow?.Close();
        _warningWindow = new OvertimeWarningWindow();
        _warningWindow.SetMessage(message);
        _warningWindow.Show();

        if (!isTest && _activeMeeting != null)
        {
            _warningShownForBookingId = _activeMeeting.BookingId;
        }
    }

    private void ShowDashboard()
    {
        if (_configWindow == null)
        {
            return;
        }

        if (!_configWindow.IsVisible)
        {
            _configWindow.Show();
        }

        _configWindow.ShowInTaskbar = true;
        _configWindow.WindowState = WindowState.Normal;
        _configWindow.Activate();
    }

    private void ExitApplication()
    {
        _isShuttingDown = true;
        if (_configWindow != null)
        {
            _configWindow.AllowClose = true;
            _configWindow.Close();
        }

        Shutdown();
    }

    private void UpdateMeetingWindowVisibility()
    {
        if (_meetingDetailsWindow == null)
        {
            return;
        }

        if (_activeMeeting == null)
        {
            _meetingDetailsWindow.Hide();
            return;
        }

        var allOthersMinimized = Current.Windows
            .Cast<Window>()
            .Where(window => window != _meetingDetailsWindow)
            .All(window => !window.IsVisible || window.WindowState == WindowState.Minimized);

        if (!allOthersMinimized)
        {
            _meetingDetailsWindow.Hide();
            return;
        }

        var workArea = SystemParameters.WorkArea;
        _meetingDetailsWindow.Left = workArea.Right - _meetingDetailsWindow.Width - 16;
        _meetingDetailsWindow.Top = workArea.Bottom - _meetingDetailsWindow.Height - 16;

        if (!_meetingDetailsWindow.IsVisible)
        {
            _meetingDetailsWindow.Show();
        }

        _meetingDetailsWindow.SendToBottom();
    }
}
