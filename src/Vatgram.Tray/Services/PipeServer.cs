using System.IO.Pipes;
using Vatgram.Shared;

namespace Vatgram.Tray.Services;

public sealed class PipeServer : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private NamedPipeServerStream? _currentPipe;
    private Task? _runLoop;

    public event Action<IpcMessage>? MessageReceived;
    public event Action<bool>? ConnectionChanged;

    public bool IsConnected { get; private set; }

    public void Start()
    {
        _runLoop = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    public async Task SendAsync(IpcMessage message)
    {
        var pipe = _currentPipe;
        if (pipe is null || !pipe.IsConnected) return;
        try { await IpcCodec.WriteAsync(pipe, message, _cts.Token); }
        catch { /* will reconnect */ }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(
                    IpcConstants.PipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(ct);
                _currentPipe = pipe;
                SetConnected(true);

                while (pipe.IsConnected && !ct.IsCancellationRequested)
                {
                    var msg = await IpcCodec.ReadAsync(pipe, ct);
                    if (msg is null) break;
                    try { MessageReceived?.Invoke(msg); }
                    catch { /* swallow handler errors */ }
                }
            }
            catch (OperationCanceledException) { return; }
            catch { /* loop and retry */ }
            finally
            {
                SetConnected(false);
                _currentPipe = null;
                try { pipe?.Dispose(); } catch { }
            }

            try { await Task.Delay(500, ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    private void SetConnected(bool value)
    {
        if (IsConnected == value) return;
        IsConnected = value;
        try { ConnectionChanged?.Invoke(value); } catch { }
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _currentPipe?.Dispose(); } catch { }
        try { _runLoop?.Wait(2000); } catch { }
        _cts.Dispose();
    }
}
