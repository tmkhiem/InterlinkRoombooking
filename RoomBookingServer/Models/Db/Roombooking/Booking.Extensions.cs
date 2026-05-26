namespace RoomBookingServer.Models.Db.Roombooking;

public partial class Booking
{
    public const int Booked = 0;
    public const int ConfirmationWait = 1;
    public const int Unused = 2;
    public const int ConfirmationRejected = 254;
    public const int Cancelled = 255;

    public bool IsOverlapping(Booking another)
    {
        return Date == another.Date
               && Room == another.Room
               && StartTime < another.EndTime
               && another.StartTime < EndTime;
    }
}
