using System;
using System.Reflection;
using System.Threading;
using RossCarlson.Vatsim.Vpilot.Plugins;
using RossCarlson.Vatsim.Vpilot.Plugins.Events;
using Vatgram.Shared;

namespace Vatgram.Plugin;

public sealed class VatgramPlugin : IPlugin
{
    public string Name => "vatGram";

    private IBroker? _broker;
    private PipeBridge? _bridge;
    private CancellationTokenSource? _cts;

    public void Initialize(IBroker broker)
    {
        _broker = broker;
        _cts = new CancellationTokenSource();

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        _bridge = new PipeBridge(version, OnCommand, msg => SafeDebug(msg));
        _bridge.Start(_cts.Token);

        broker.PrivateMessageReceived += OnPrivateMessage;
        broker.RadioMessageReceived += OnRadioMessage;
        broker.BroadcastMessageReceived += OnBroadcastMessage;
        broker.SelcalAlertReceived += OnSelcal;
        broker.NetworkConnected += OnNetworkConnected;
        broker.NetworkDisconnected += OnNetworkDisconnected;
        broker.MetarReceived += OnMetar;
        broker.AtisReceived += OnAtis;
        broker.SessionEnded += OnSessionEnded;

        SafeDebug("vatGram plugin initialized.");
    }

    private void OnPrivateMessage(object sender, PrivateMessageReceivedEventArgs e)
        => _bridge?.Send(new PrivateMessageEvent(e.From, e.Message));

    private void OnRadioMessage(object sender, RadioMessageReceivedEventArgs e)
        => _bridge?.Send(new RadioMessageEvent(e.Frequencies, e.From, e.Message));

    private void OnBroadcastMessage(object sender, BroadcastMessageReceivedEventArgs e)
        => _bridge?.Send(new BroadcastMessageEvent(e.From, e.Message));

    private void OnSelcal(object sender, SelcalAlertReceivedEventArgs e)
        => _bridge?.Send(new SelcalEvent(e.Frequencies, e.From));

    private void OnNetworkConnected(object sender, NetworkConnectedEventArgs e)
        => _bridge?.Send(new NetworkConnectedEvent(e.Cid, e.Callsign, e.TypeCode, e.SelcalCode, e.ObserverMode));

    private void OnNetworkDisconnected(object sender, EventArgs e)
        => _bridge?.Send(new NetworkDisconnectedEvent());

    private void OnMetar(object sender, MetarReceivedEventArgs e)
        => _bridge?.Send(new MetarReceivedEvent(e.Metar));

    private void OnAtis(object sender, AtisReceivedEventArgs e)
        => _bridge?.Send(new AtisReceivedEvent(e.From, e.Lines));

    private void OnSessionEnded(object sender, EventArgs e)
    {
        try { _cts?.Cancel(); } catch { }
        _bridge?.Dispose();
    }

    private void OnCommand(IpcMessage msg)
    {
        if (_broker == null) return;
        try
        {
            switch (msg)
            {
                case SendPrivateMessageCommand pm:
                    _broker.SendPrivateMessage(pm.To, pm.Message);
                    break;
                case SendRadioMessageCommand rm:
                    _broker.SendRadioMessage(rm.Message);
                    break;
                case RequestMetarCommand rmet:
                    _broker.RequestMetar(rmet.Station);
                    break;
                case RequestAtisCommand ratis:
                    _broker.RequestAtis(ratis.Callsign);
                    break;
                case RequestDisconnectCommand:
                    _broker.RequestDisconnect();
                    break;
                case SquawkIdentCommand:
                    _broker.SquawkIdent();
                    break;
                case SetModeCCommand mc:
                    _broker.SetModeC(mc.Enabled);
                    break;
            }
        }
        catch (Exception ex)
        {
            SafeDebug("Command failed: " + ex.Message);
        }
    }

    private void SafeDebug(string message)
    {
        try { _broker?.PostDebugMessage("[vatGram] " + message); } catch { }
    }
}
