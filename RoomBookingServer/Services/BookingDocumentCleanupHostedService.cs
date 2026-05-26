namespace RoomBookingServer.Services;

/// <summary>
/// Ephemeral document cleanup policy: clear uploaded booking documents on server startup and shutdown.
/// </summary>
public sealed class BookingDocumentCleanupHostedService(
    IBookingDocumentStorage documentStorage) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return documentStorage.CleanupAllAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return documentStorage.CleanupAllAsync(cancellationToken);
    }
}
