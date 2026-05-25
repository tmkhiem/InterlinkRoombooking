using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace RoomBookingBackend.Models;

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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=Db");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(e => e.Email);

            entity.Property(e => e.Email).HasMaxLength(64);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("Booking");

            entity.Property(e => e.Creator).HasMaxLength(50);
            entity.Property(e => e.EndTime).HasPrecision(0);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Note).HasMaxLength(1024);
            entity.Property(e => e.StartTime).HasPrecision(0);
            entity.Property(e => e.Title).HasMaxLength(1024);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
