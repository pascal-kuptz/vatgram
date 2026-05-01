using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Vatgram.Shared;

namespace Vatgram.Plugin;

internal sealed class PipeBridge : IDisposable
{
    private readonly string _pluginVersion;
    private readonly Action<IpcMessage> _onCommand;
    private readonly Action<string> _log;
    private readonly BlockingCollection<IpcMessage> _outbound = new();
    private CancellationTokenSource? _cts;
    private Task? _runLoop;

    public PipeBridge(string pluginVersion, Action<IpcMessage> onCommand, Action<string> log)
    {
        _pluginVersion = pluginVersion;
        _onCommand = onCommand;
        _log = log;
    }

    public void Start(CancellationToken externalCt)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        _runLoop = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    public void Send(IpcMessage message)
    {
        if (!_outbound.IsAddingCompleted) _outbound.Add(message);
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var connCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Task? sendTask = null;
            Task? recvTask = null;
            try
            {
                using var pipe = new NamedPipeClientStream(".", IpcConstants.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(5000, ct).ConfigureAwait(false);

                await IpcCodec.WriteAsync(pipe, new HelloMessage(IpcConstants.ProtocolVersion, _pluginVersion), connCts.Token).ConfigureAwait(false);

                sendTask = Task.Run(() => SendLoopAsync(pipe, connCts.Token));
                recvTask = Task.Run(() => ReceiveLoopAsync(pipe, connCts.Token));
                await Task.WhenAny(sendTask, recvTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
            catch (TimeoutException) { /* tray not running yet */ }
            catch (Exception ex) { _log("Bridge error: " + ex.Message); }
            finally
            {
                // Tear down the surviving task before disposing the pipe so we
                // never leave a zombie SendLoopAsync writing to a dead pipe.
                try { connCts.Cancel(); } catch { }
                if (sendTask != null) try { await sendTask.ConfigureAwait(false); } catch { }
                if (recvTask != null) try { await recvTask.ConfigureAwait(false); } catch { }
            }

            try { await Task.Delay(2000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task SendLoopAsync(NamedPipeClientStream pipe, CancellationToken ct)
    {
        foreach (var msg in _outbound.GetConsumingEnumerable(ct))
        {
            await IpcCodec.WriteAsync(pipe, msg, ct).ConfigureAwait(false);
        }
    }

    private async Task ReceiveLoopAsync(NamedPipeClientStream pipe, CancellationToken ct)
    {
        while (pipe.IsConnected && !ct.IsCancellationRequested)
        {
            var msg = await IpcCodec.ReadAsync(pipe, ct).ConfigureAwait(false);
            if (msg == null) return;
            try { _onCommand(msg); } catch (Exception ex) { _log("Command handler error: " + ex.Message); }
        }
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _outbound.CompleteAdding(); } catch { }
        try { _runLoop?.Wait(2000); } catch { }
        _outbound.Dispose();
        _cts?.Dispose();
    }
}
