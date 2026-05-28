using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RoomBookingClient;

public partial class MeetingDetailsWindow : Window
{
    private string? _folderPath;

    public MeetingDetailsWindow()
    {
        InitializeComponent();
    }

    public void SetMeeting(MeetingSnapshot? snapshot)
    {
        if (snapshot == null)
        {
            TitleText.Text = "No active meeting";
            TimeText.Text = string.Empty;
            HostText.Text = string.Empty;
            NoteText.Text = string.Empty;
            _folderPath = null;
            return;
        }

        TitleText.Text = snapshot.Title;
        TimeText.Text = $"Time: {snapshot.StartLocal:HH:mm} - {snapshot.EndLocal:HH:mm}";
        HostText.Text = $"Booked by: {snapshot.CreatorDisplay}";
        NoteText.Text = string.IsNullOrWhiteSpace(snapshot.Note) ? "" : $"Note: {snapshot.Note}";
        _folderPath = snapshot.DownloadFolder;
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_folderPath) || !System.IO.Directory.Exists(_folderPath))
        {
            return;
        }

        Process.Start("explorer.exe", _folderPath);
    }

    public void SendToBottom()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private static readonly IntPtr HWND_BOTTOM = new(1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
