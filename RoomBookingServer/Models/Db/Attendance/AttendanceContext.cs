using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace RoomBookingServer.Models.Db.Attendance;

public partial class AttendanceContext : DbContext
{
    public AttendanceContext()
    {
    }

    public AttendanceContext(DbContextOptions<AttendanceContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Employee> Employees { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=Attendance");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_Employee");

            entity.Property(e => e.Id)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("ID");
            entity.Property(e => e.DeviceUserId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Email)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(1000);
            entity.Property(e => e.ManagerId)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("ManagerID");
            entity.Property(e => e.MaxBorrowDays).HasColumnType("decimal(10, 5)");
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.OrganizationUnitId).HasColumnName("OrganizationUnitID");
            entity.Property(e => e.Password).IsUnicode(false);
            entity.Property(e => e.SeniorityBonusLeaveDays).HasColumnType("decimal(10, 5)");
            entity.Property(e => e.TotalBorrowedLeaveDays).HasColumnType("decimal(10, 5)");

            entity.HasOne(d => d.Manager).WithMany(p => p.InverseManager)
                .HasForeignKey(d => d.ManagerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Employees_Employees");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
