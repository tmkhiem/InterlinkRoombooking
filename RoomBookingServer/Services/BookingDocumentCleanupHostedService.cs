namespace RoomBookingServer.Services;

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
