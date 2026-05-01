using System.Runtime.InteropServices;
using SimConnect.NET;

namespace Vatgram.Tray.Services;

public sealed class SimConnectService : IDisposable
{
    private const string AppName = "vatgram";
    private const uint OBJECT_USER = 0;
    private const uint EVENT_FLAG_GROUPID_IS_PRIORITY = 0x00000010;
    private const uint EVENT_PRIORITY_HIGHEST = 1;

    private enum EventId : uint
    {
        Com1SetHz = 1001,
        Com2SetHz = 1002,
        XpndrSet = 1003,
    }

    private readonly SimConnectClient _client = new(AppName);
    private readonly CancellationTokenSource _cts = new();
    private Task? _connectLoop;
    private Task? _messagePump;
    private volatile bool _eventsMapped;

    public event Action<bool>? ConnectionChanged;
    public bool IsConnected => _client.IsConnected;

    public void Start()
    {
        _client.AutoReconnectEnabled = true;
        _client.ReconnectDelay = TimeSpan.FromSeconds(3);
        _client.MaxReconnectAttempts = int.MaxValue;
        _client.ConnectionStatusChanged += (_, _) =>
        {
            if (_client.IsConnected) MapEvents(); else _eventsMapped = false;
            try { ConnectionChanged?.Invoke(_client.IsConnected); } catch { }
        };

        _connectLoop = Task.Run(InitialConnectAsync);
        _messagePump = Task.Run(MessagePumpAsync);
    }

    private void MapEvents()
    {
        if (_eventsMapped || !_client.IsConnected) return;
        try
        {
            var h = _client.Handle;
            Native.MapClientEventToSimEvent(h, (uint)EventId.Com1SetHz, "COM_RADIO_SET_HZ");
            Native.MapClientEventToSimEvent(h, (uint)EventId.Com2SetHz, "COM2_RADIO_SET_HZ");
            Native.MapClientEventToSimEvent(h, (uint)EventId.XpndrSet, "XPNDR_SET");
            _eventsMapped = true;
        }
        catch { _eventsMapped = false; }
    }

    public Task SetCom1HzAsync(uint hz) => Transmit(EventId.Com1SetHz, hz);
    public Task SetCom2HzAsync(uint hz) => Transmit(EventId.Com2SetHz, hz);
    public Task SetTransponderBcoAsync(uint bco) => Transmit(EventId.XpndrSet, bco);

    private Task Transmit(EventId evt, uint data)
    {
        if (!_client.IsConnected) return Task.CompletedTask;
        if (!_eventsMapped) MapEvents();
        try
        {
            Native.TransmitClientEventEx1(
                _client.Handle, OBJECT_USER, (uint)evt,
                EVENT_PRIORITY_HIGHEST, EVENT_FLAG_GROUPID_IS_PRIORITY,
                data, 0, 0, 0, 0);
        }
        catch { /* per-command best-effort */ }
        return Task.CompletedTask;
    }

    private async Task InitialConnectAsync()
    {
        while (!_cts.IsCancellationRequested && !_client.IsConnected)
        {
            try
            {
                await _client.ConnectAsync(IntPtr.Zero, 0, 0, _cts.Token);
                return;
            }
            catch (OperationCanceledException) { return; }
            catch
            {
                try { await Task.Delay(3000, _cts.Token); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private async Task MessagePumpAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                if (_client.IsConnected) await _client.ProcessNextMessageAsync(_cts.Token);
                else await Task.Delay(100, _cts.Token);
            }
            catch (OperationCanceledException) { return; }
            catch { /* keep pumping */ }
        }
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _client.DisconnectAsync().Wait(1000); } catch { }
        try { _connectLoop?.Wait(2000); } catch { }
        try { _messagePump?.Wait(2000); } catch { }
        try { _client.Dispose(); } catch { }
        _cts.Dispose();
    }

    private static class Native
    {
        [DllImport("SimConnect.dll", CharSet = CharSet.Ansi, EntryPoint = "SimConnect_MapClientEventToSimEvent")]
        public static extern int MapClientEventToSimEvent(IntPtr hSimConnect, uint EventID, string EventName);

        [DllImport("SimConnect.dll", EntryPoint = "SimConnect_TransmitClientEvent_EX1")]
        public static extern int TransmitClientEventEx1(IntPtr hSimConnect, uint ObjectID, uint EventID, uint GroupID, uint Flags, uint d0, uint d1, uint d2, uint d3, uint d4);
    }
}
