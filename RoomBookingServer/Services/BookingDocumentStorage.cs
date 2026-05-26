using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RoomBookingServer.Models.Db.Roombooking;

namespace RoomBookingServer.Services;

public interface IBookingDocumentStorage
{
    string RootPath { get; }

    Task<StoredDocumentInfo> SaveAsync(
        Stream fileStream,
        string originalFileName,
        string? contentType,
        string uploadedBy,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string storagePath, CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);

    Task CleanupAllAsync(CancellationToken cancellationToken = default);
}

public sealed record StoredDocumentInfo(
    string OriginalFileName,
    string StoredFileName,
    string StoragePath,
    string? ContentType,
    long FileSizeBytes,
    DateTime UploadedAtUtc,
    DateTime ExpiresAtUtc);

public sealed class BookingDocumentStorage(
    IWebHostEnvironment environment,
    IDbContextFactory<RoombookingContext> dbContextFactory,
    ILogger<BookingDocumentStorage> logger) : IBookingDocumentStorage
{
    private const string StorageFolderName = "booking-documents";

    public string RootPath => Path.Combine(environment.ContentRootPath, StorageFolderName);

    public async Task<StoredDocumentInfo> SaveAsync(
        Stream fileStream,
        string originalFileName,
        string? contentType,
        string uploadedBy,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(RootPath);

        var safeFileName = Path.GetFileName(originalFileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = "document.bin";
        }

        var extension = Path.GetExtension(safeFileName);
        var timestamp = DateTime.UtcNow;
        var random = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var storedFileName = $"{timestamp:yyyyMMddHHmmssfff}-{random}{extension}";
        var fullPath = Path.Combine(RootPath, storedFileName);

        await using var targetStream = File.Create(fullPath);
        await fileStream.CopyToAsync(targetStream, cancellationToken);

        var fileSize = targetStream.Length;
        var relativePath = Path.Combine(StorageFolderName, storedFileName).Replace('\\', '/');
        return new StoredDocumentInfo(
            safeFileName,
            storedFileName,
            relativePath,
            string.IsNullOrWhiteSpace(contentType) ? null : contentType.Trim(),
            fileSize,
            timestamp,
            timestamp.AddDays(1));
    }

    public Task<bool> DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return Task.FromResult(false);
        }

        var fullPath = ResolveStoragePath(storagePath);
        if (!IsPathInRoot(fullPath) || !File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        File.Delete(fullPath);
        return Task.FromResult(true);
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        var fullPath = ResolveStoragePath(storagePath);
        if (!IsPathInRoot(fullPath) || !File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult<Stream?>(stream);
    }

    public async Task CleanupAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }

            Directory.CreateDirectory(RootPath);

            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var documents = await db.BookingDocuments.ToListAsync(cancellationToken);
            if (documents.Count == 0)
            {
                return;
            }

            db.BookingDocuments.RemoveRange(documents);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to clean up booking documents.");
        }
    }

    private string ResolveStoragePath(string storagePath)
    {
        var normalized = storagePath.Replace('\\', '/').TrimStart('/');
        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, normalized));
    }

    private bool IsPathInRoot(string fullPath)
    {
        var root = Path.GetFullPath(RootPath);
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
