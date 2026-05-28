using System;
using System.Collections.Generic;
using System.Windows;

namespace RoomBookingClient;

public partial class ConfigWindow : Window
{
    private const int MaxLogEntries = 300;
    private readonly Queue<string> _logEntries = new();

    public bool AllowClose { get; set; }

    public event EventHandler<ClientSettings>? SaveRequested;
    public event EventHandler? RefreshRequested;
    public event EventHandler? TestWarningRequested;

    public ConfigWindow(ClientSettings settings)
    {
        InitializeComponent();
        ApplySettings(settings);
        SetStatus("Đang chạy dưới khay hệ thống.");
    }

    public void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }

    public void AppendLog(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logEntries.Enqueue(entry);
        while (_logEntries.Count > MaxLogEntries)
        {
            _logEntries.Dequeue();
        }

        LogTextBox.Text = string.Join(Environment.NewLine, _logEntries);
        LogTextBox.ScrollToEnd();
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
            SetStatus("Số phòng phải là 1, 2 hoặc 3.");
            return;
        }

        var settings = new ClientSettings
        {
            ServerUrl = ServerUrlTextBox.Text.Trim(),
            RoomNumber = room,
            ApiKey = ApiKeyTextBox.Text.Trim()
        };

        SaveRequested?.Invoke(this, settings);
        SetStatus("Đã lưu. Đang đồng bộ cuộc họp hiện tại...");
        HideToTray();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
        SetStatus("Đang làm mới...");
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
