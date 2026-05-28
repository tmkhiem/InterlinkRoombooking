using System;
using System.IO;

namespace RoomBookingClient;

public static class TempMeetingStorage
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "InterlinkRoomBookingClient");

    public static string CreateMeetingFolder(int bookingId)
    {
        DeleteCurrentMeetingFolder();

        var path = Path.Combine(Root, $"booking-{bookingId}-{DateTime.Now:yyyyMMddHHmmss}");
        Directory.CreateDirectory(path);
        return path;
    }

    public static void DeleteCurrentMeetingFolder()
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        foreach (var dir in Directory.GetDirectories(Root))
        {
            TryDeleteDirectory(dir);
        }
    }

    public static void CleanupRoot()
    {
        if (Directory.Exists(Root))
        {
            TryDeleteDirectory(Root);
        }

        Directory.CreateDirectory(Root);
    }

    public static void DeleteRoot()
    {
        if (Directory.Exists(Root))
        {
            TryDeleteDirectory(Root);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, true);
        }
        catch
        {
            // best effort cleanup
        }
    }
}
