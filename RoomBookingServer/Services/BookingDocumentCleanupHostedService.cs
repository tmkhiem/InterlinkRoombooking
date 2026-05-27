using Microsoft.EntityFrameworkCore;
using RoomBookingServer.Models.Db.Roombooking;

namespace RoomBookingServer.Services;

/// <summary>
/// Cleans up booking documents that are no longer valid.
/// </summary>
public sealed class BookingDocumentCleanupHostedService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<BookingDocumentCleanupHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupExpiredAsync(stoppingToken);

            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CleanupExpiredAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RoombookingContext>>();
            var documentStorage = scope.ServiceProvider.GetRequiredService<IBookingDocumentStorage>();
            var today = DateOnly.FromDateTime(DateTime.Today);

            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var staleDocuments = await db.BookingDocuments
                .Where(d =>
                    d.DeletedAtUtc != null ||
                    d.RoomBooking == null ||
                    d.RoomBooking.Date < today)
                .Select(d => new
                {
                    d.BookingDocumentId,
                    d.StoragePath
                })
                .ToListAsync(cancellationToken);

            if (staleDocuments.Count == 0)
            {
                return;
            }

            var removableIds = new List<long>(staleDocuments.Count);
            foreach (var document in staleDocuments)
            {
                if (!await documentStorage.DeleteAsync(document.StoragePath, cancellationToken))
                {
                    logger.LogWarning(
                        "Failed to delete stale booking document file for BookingDocumentId {BookingDocumentId}.",
                        document.BookingDocumentId);
                    continue;
                }

                removableIds.Add(document.BookingDocumentId);
            }

            if (removableIds.Count == 0)
            {
                return;
            }

            await db.BookingDocuments
                .Where(d => removableIds.Contains(d.BookingDocumentId))
                .ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean stale booking documents.");
        }
    }
}
