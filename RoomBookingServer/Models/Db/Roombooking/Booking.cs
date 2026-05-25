using System;
using System.Collections.Generic;

namespace RoomBookingServer.Models.Db.Roombooking;

public partial class Booking
{
    public int RoomBookingId { get; set; }

    /// <summary>
    /// Employee ID, should be FK to Attendance db Employees table column ID
    /// </summary>
    public string Creator { get; set; } = null!;

    public string Name { get; set; } = null!;

    /// <summary>
    /// The title of the meeting
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// 1 = Meeting room 1 (GF), 2 = Meeting room 2 (1F), 3 = Meeting room 3 (5F)
    /// </summary>
    public int Room { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int State { get; set; }

    public string? Note { get; set; }

    public virtual ICollection<BookingDocument> BookingDocuments { get; set; } = new List<BookingDocument>();
}
