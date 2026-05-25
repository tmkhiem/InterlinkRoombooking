using System;
using System.Collections.Generic;

namespace RoomBookingServer.Models.Db.Attendance;

public partial class Employee
{
    public string Id { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string DeviceUserId { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public int OrganizationUnitId { get; set; }

    public string ManagerId { get; set; } = null!;

    public string? Email { get; set; }

    public decimal SeniorityBonusLeaveDays { get; set; }

    public decimal TotalBorrowedLeaveDays { get; set; }

    public string? Notes { get; set; }

    public decimal MaxBorrowDays { get; set; }

    public virtual ICollection<Employee> InverseManager { get; set; } = new List<Employee>();

    public virtual Employee Manager { get; set; } = null!;
}
