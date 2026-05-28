namespace RoomBookingServer.Options;

/// <summary>
/// Configuration options for booking document uploads.
/// Bound from the "DocumentUpload" section in appsettings.json.
/// </summary>
public sealed class DocumentUploadOptions
{
    public const string SectionName = "DocumentUpload";

    /// <summary>Maximum size per uploaded file, in megabytes. Default: 25 MB.</summary>
    public int MaxFileSizeMB { get; set; } = 25;

    /// <summary>Maximum number of files that can be attached to a single booking. Default: 10.</summary>
    public int MaxFileCount { get; set; } = 10;

    /// <summary>Derived maximum file size in bytes.</summary>
    public long MaxFileSizeBytes => (long)MaxFileSizeMB * 1024 * 1024;
}
