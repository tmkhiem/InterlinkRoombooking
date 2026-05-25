using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace RoomBookingServer.Models.Db.Roombooking;

public partial class RoombookingContext : DbContext
{
    public RoombookingContext()
    {
    }

    public RoombookingContext(DbContextOptions<RoombookingContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Admin> Admins { get; set; }

    public virtual DbSet<Booking> Bookings { get; set; }

    public virtual DbSet<BookingDocument> BookingDocuments { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=Roombooking");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(e => e.Email);

            entity.Property(e => e.Email).HasMaxLength(64);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.RoomBookingId);

            entity.ToTable("Booking");

            entity.Property(e => e.Creator)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasComment("Employee ID, should be FK to Attendance db Employees table column ID");
            entity.Property(e => e.EndTime).HasPrecision(0);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Note).HasMaxLength(1024);
            entity.Property(e => e.Room).HasComment("1 = Meeting room 1 (GF), 2 = Meeting room 2 (1F), 3 = Meeting room 3 (5F)");
            entity.Property(e => e.StartTime).HasPrecision(0);
            entity.Property(e => e.Title)
                .HasMaxLength(1024)
                .HasComment("The title of the meeting");
        });

        modelBuilder.Entity<BookingDocument>(entity =>
        {
            entity.ToTable("BookingDocument");

            entity.Property(e => e.ContentType).HasMaxLength(255);
            entity.Property(e => e.DeletedAtUtc).HasPrecision(0);
            entity.Property(e => e.ExpiresAtUtc).HasPrecision(0);
            entity.Property(e => e.OriginalFileName).HasMaxLength(255);
            entity.Property(e => e.StoragePath).HasMaxLength(1024);
            entity.Property(e => e.StoredFileName).HasMaxLength(255);
            entity.Property(e => e.UploadedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UploadedBy)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.RoomBooking).WithMany(p => p.BookingDocuments)
                .HasForeignKey(d => d.RoomBookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BookingDocument_Booking");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
