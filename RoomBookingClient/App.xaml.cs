using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace RoomBookingClient;

public partial class App : Application
{
    private const int WindowVisibilityCheckIntervalSeconds = 5;
    private const int MeetingOvertimeCheckIntervalSeconds = 20;
    private const double MeetingWindowWidthRatio = 0.48;
    private const double MeetingWindowHeightRatio = 0.35;
    private const double MeetingWindowMinWidth = 600;
    private const double MeetingWindowMaxWidth = 980;
    private const double MeetingWindowMinHeight = 260;
    private const double MeetingWindowMaxHeight = 420;
    private const double MeetingWindowMargin = 16;

    private readonly SettingsStore _settingsStore = new();
    private readonly MeetingSyncService _meetingSyncService = new();
    private readonly DispatcherTimer _windowVisibilityTimer;
    private readonly DispatcherTimer _meetingWarningTimer;
    private readonly SingleInstanceManager _singleInstanceManager;
    private readonly List<string> _pendingLogMessages = new();

    private Forms.NotifyIcon? _trayIcon;
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
            Interval = TimeSpan.FromSeconds(WindowVisibilityCheckIntervalSeconds)
        };
        _windowVisibilityTimer.Tick += (_, _) => UpdateMeetingWindowVisibility();

        _meetingWarningTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(MeetingOvertimeCheckIntervalSeconds)
        };
        _meetingWarningTimer.Tick += (_, _) => CheckMeetingOvertime();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LogVerbose("Khởi động ứng dụng khách.");

        TempMeetingStorage.DeleteRoot();
        LogVerbose("Đã xóa dữ liệu cuộc họp tạm.");

        if (!_singleInstanceManager.TryStart(() => Dispatcher.Invoke(ShowDashboard)))
        {
            LogVerbose("Phát hiện phiên đang chạy, gửi tín hiệu mở bảng điều khiển.");
            _singleInstanceManager.SignalRunningInstance();
            Shutdown();
            return;
        }

        _settings = _settingsStore.Load();
        LogVerbose($"Đã tải cấu hình. Máy chủ='{_settings.ServerUrl}', phòng={_settings.RoomNumber}.");

        _configWindow = new ConfigWindow(_settings)
        {
            Visibility = Visibility.Hidden,
            ShowInTaskbar = false,
            WindowState = WindowState.Minimized
        };

        _configWindow.SaveRequested += OnConfigSaveRequested;
        _configWindow.RefreshRequested += (_, _) =>
        {
            LogVerbose("Người dùng yêu cầu làm mới thủ công.");
            _meetingSyncService.TriggerRefresh();
        };
        _configWindow.TestWarningRequested += (_, _) => ShowWarning(true);
        FlushPendingLogs();
        _configWindow.AppendLog("Bảng cấu hình đã sẵn sàng.");

        _meetingDetailsWindow = new MeetingDetailsWindow();
        _meetingDetailsWindow.Hide();
        LogVerbose("Đã khởi tạo cửa sổ chi tiết cuộc họp.");

        InitializeTrayIcon();

        _meetingSyncService.MeetingUpdated += OnMeetingUpdated;
        _meetingSyncService.ErrorOccurred += OnMeetingError;
        _meetingSyncService.LogOccurred += (_, message) => LogVerbose(message);
        _meetingSyncService.Start(() => _settings);
        LogVerbose("Đã bắt đầu dịch vụ đồng bộ cuộc họp.");

        _windowVisibilityTimer.Start();
        _meetingWarningTimer.Start();
        LogVerbose("Đã bật các bộ đếm thời gian nền.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LogVerbose("Đang tắt ứng dụng khách.");
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
            Text = "Ứng dụng đặt phòng Interlink"
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Mở bảng điều khiển", null, (_, _) => ShowDashboard());
        menu.Items.Add("Thoát", null, (_, _) => ExitApplication());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowDashboard();
        LogVerbose("Đã khởi tạo biểu tượng khay hệ thống.");
    }

    private void OnConfigSaveRequested(object? sender, ClientSettings settings)
    {
        LogVerbose($"Đang lưu cấu hình mới. Máy chủ='{settings.ServerUrl}', phòng={settings.RoomNumber}.");
        _settings = settings;
        _settingsStore.Save(settings);
        _meetingSyncService.TriggerRefresh();
        LogVerbose("Đã lưu cấu hình và yêu cầu đồng bộ ngay.");
    }

    private void OnMeetingUpdated(object? sender, MeetingSnapshot? snapshot)
    {
        Dispatcher.Invoke(() =>
        {
            _activeMeeting = snapshot;
            _meetingDetailsWindow?.SetMeeting(snapshot);
            LogVerbose(snapshot == null
                ? "Không có cuộc họp đang diễn ra."
                : $"Đã cập nhật cuộc họp '{snapshot.Title}' từ {snapshot.StartLocal:HH:mm} đến {snapshot.EndLocal:HH:mm}.");

            if (snapshot == null)
            {
                _warningShownForBookingId = null;
                _warningWindow?.Close();
                _warningWindow = null;
                LogVerbose("Đã đặt lại trạng thái cảnh báo quá giờ.");
            }

            UpdateMeetingWindowVisibility();
        });
    }

    private void OnMeetingError(object? sender, string message)
    {
        LogVerbose(message);
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
            ? "Cảnh báo thử nghiệm: thông báo cuộc họp quá giờ."
            : "Cuộc họp hiện tại đã vượt quá thời lượng được cấp.";

        _warningWindow?.Close();
        _warningWindow = new OvertimeWarningWindow();
        _warningWindow.SetMessage(message);
        _warningWindow.Show();
        LogVerbose(isTest ? "Đã hiển thị cảnh báo thử nghiệm." : "Đã hiển thị cảnh báo cuộc họp quá giờ.");

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
            LogVerbose("Đã hiển thị bảng điều khiển.");
        }

        _configWindow.ShowInTaskbar = true;
        _configWindow.WindowState = WindowState.Normal;
        _configWindow.Activate();
    }

    private void ExitApplication()
    {
        LogVerbose("Người dùng yêu cầu thoát ứng dụng.");
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
        _meetingDetailsWindow.Width = Math.Max(MeetingWindowMinWidth, Math.Min(MeetingWindowMaxWidth, workArea.Width * MeetingWindowWidthRatio));
        _meetingDetailsWindow.Height = Math.Max(MeetingWindowMinHeight, Math.Min(MeetingWindowMaxHeight, workArea.Height * MeetingWindowHeightRatio));
        _meetingDetailsWindow.Left = workArea.Left + MeetingWindowMargin;
        _meetingDetailsWindow.Top = workArea.Top + MeetingWindowMargin;

        if (!_meetingDetailsWindow.IsVisible)
        {
            _meetingDetailsWindow.Show();
            LogVerbose("Đã hiển thị cửa sổ chi tiết cuộc họp.");
        }

        _meetingDetailsWindow.SendToBottom();
    }

    private void LogVerbose(string message)
    {
        if (Dispatcher.CheckAccess())
        {
            if (_configWindow == null)
            {
                _pendingLogMessages.Add(message);
                return;
            }

            _configWindow.AppendLog(message);
            return;
        }

        Dispatcher.Invoke(() =>
        {
            if (_configWindow == null)
            {
                _pendingLogMessages.Add(message);
                return;
            }

            _configWindow.AppendLog(message);
        });
    }

    private void FlushPendingLogs()
    {
        if (_configWindow == null || _pendingLogMessages.Count == 0)
        {
            return;
        }

        foreach (var pendingMessage in _pendingLogMessages)
        {
            _configWindow.AppendLog(pendingMessage);
        }

        _pendingLogMessages.Clear();
    }
}
