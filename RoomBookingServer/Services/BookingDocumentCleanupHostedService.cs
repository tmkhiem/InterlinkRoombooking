namespace RoomBookingServer.Services;

/// <summary>
/// Ephemeral document cleanup policy: clear uploaded booking documents on server startup and shutdown.
/// </summary>
public sealed class BookingDocumentCleanupHostedService(
    IServiceScopeFactory serviceScopeFactory) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return CleanupAllAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return CleanupAllAsync(cancellationToken);
    }

    private async Task CleanupAllAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var documentStorage = scope.ServiceProvider.GetRequiredService<IBookingDocumentStorage>();
        await documentStorage.CleanupAllAsync(cancellationToken);
    }
}
