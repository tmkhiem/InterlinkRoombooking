namespace RoomBookingBackend.Models;

public partial class Booking
{
    public bool IsOverlapping(Booking another)
    {
        return
            Date == another.Date &&
            Room == another.Room &&
            (
                StartTime < another.EndTime &
                another.StartTime < EndTime
            );

        // parsedstarttime = 12
        // another.parsedstarttime = 13
        // another.parsedendtime = 14
        // parsedendtime = 15

        // ParsedStartTime < another.ParsedEndTime --> 12 < 14 true
        // another.ParsedStartTime < ParsedEndTime --> 13 < 15 true

        // parsedstarttime = 8
        // another.parsedstarttime = 13
        // another.parsedendtime = 14
        // parsedendtime = 9

        // ParsedStartTime < another.ParsedEndTime --> 8 < 14 true
        // another.ParsedStartTime < ParsedEndTime --> 13 < 9 false
    }

    // 0: booked, 1: confirmation wait, 2: unused, 254: confirmation rejected, 255: cancelled
    public const int Booked = 0;
    public const int ConfirmationWait = 1;
    public const int Unused = 2;
    public const int ConfirmationRejected = 254;
    public const int Cancelled = 255;

    public string ToTelegramMessage()
    {
        return
            "• Title: " + Title + '\n' +
            "• Name: " + Name + '\n' +
            "• Email: " + this.Creator + '\n' +
            "• Room: " + Room + '\n' +
            "• Date: " + Date.ToString("yyyy-MM-dd") + '\n' +
            "• Start time: " + StartTime + '\n' +
            "• End time: " + EndTime;
    }
}