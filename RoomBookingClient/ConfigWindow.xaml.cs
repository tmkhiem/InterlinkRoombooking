using System;
using System.Windows;

namespace RoomBookingClient;

public partial class ConfigWindow : Window
{
    public bool AllowClose { get; set; }

    public event EventHandler<ClientSettings>? SaveRequested;
    public event EventHandler? RefreshRequested;
    public event EventHandler? TestWarningRequested;

    public ConfigWindow(ClientSettings settings)
    {
        InitializeComponent();
        ApplySettings(settings);
        SetStatus("Running in tray mode.");
    }

    public void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }

    private void ApplySettings(ClientSettings settings)
    {
        ServerUrlTextBox.Text = settings.ServerUrl;
        RoomNumberTextBox.Text = settings.RoomNumber.ToString();
        ApiKeyTextBox.Text = settings.ApiKey;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RoomNumberTextBox.Text.Trim(), out var room) || room < 1 || room > 3)
        {
            SetStatus("Room number must be 1, 2, or 3.");
            return;
        }

        var settings = new ClientSettings
        {
            ServerUrl = ServerUrlTextBox.Text.Trim(),
            RoomNumber = room,
            ApiKey = ApiKeyTextBox.Text.Trim()
        };

        SaveRequested?.Invoke(this, settings);
        SetStatus("Saved. Syncing current meeting...");
        HideToTray();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
        SetStatus("Refreshing...");
    }

    private void TestWarningButton_Click(object sender, RoutedEventArgs e)
    {
        TestWarningRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        if (WindowState == WindowState.Minimized)
        {
            HideToTray();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (AllowClose)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();
        WindowState = WindowState.Minimized;
    }
}
