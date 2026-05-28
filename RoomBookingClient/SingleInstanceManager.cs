using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace RoomBookingClient;

public sealed class SingleInstanceManager : IDisposable
{
    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly Mutex _mutex;

    private bool _ownsMutex;
    private bool _disposed;
    private CancellationTokenSource? _pipeServerCts;

    public SingleInstanceManager(string mutexName, string pipeName)
    {
        _mutexName = mutexName;
        _pipeName = pipeName;
        _mutex = new Mutex(false, _mutexName);
    }

    public bool TryStart(Action onSignalReceived)
    {
        _ownsMutex = _mutex.WaitOne(0, false);
        if (!_ownsMutex)
        {
            return false;
        }

        _pipeServerCts = new CancellationTokenSource();
        _ = Task.Run(() => ListenForSignalsAsync(onSignalReceived, _pipeServerCts.Token));
        return true;
    }

    public void SignalRunningInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(1000);
            using var writer = new StreamWriter(client);
            writer.WriteLine("show");
            writer.Flush();
        }
        catch
        {
            // Ignore failures.
        }
    }

    private async Task ListenForSignalsAsync(Action onSignalReceived, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In);
                await server.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(server);
                var line = await reader.ReadLineAsync();
                if (string.Equals(line, "show", StringComparison.OrdinalIgnoreCase))
                {
                    onSignalReceived();
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                await Task.Delay(250, cancellationToken);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _pipeServerCts?.Cancel();
        _pipeServerCts?.Dispose();

        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _disposed = true;
    }
}
