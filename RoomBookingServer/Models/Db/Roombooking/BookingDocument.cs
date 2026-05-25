using System;
using System.Collections.Generic;

namespace RoomBookingServer.Models.Db.Roombooking;

public partial class BookingDocument
{
    public long BookingDocumentId { get; set; }

    public int RoomBookingId { get; set; }

    public string UploadedBy { get; set; } = null!;

    public string OriginalFileName { get; set; } = null!;

    public string StoredFileName { get; set; } = null!;

    public string StoragePath { get; set; } = null!;

    public string? ContentType { get; set; }

    public long FileSizeBytes { get; set; }

    public DateTime UploadedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public virtual Booking RoomBooking { get; set; } = null!;
}
