using System;
using System.Collections.Generic;

namespace RoomBookingBackend.Models;

public partial class Booking
{
    public int Id { get; set; }

    public string Creator { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Title { get; set; } = null!;

    public int Room { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int State { get; set; }

    public string? Note { get; set; }
}
