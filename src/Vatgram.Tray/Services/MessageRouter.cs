using System.Globalization;
using System.Text;
using Vatgram.Shared;

namespace Vatgram.Tray.Services;

public sealed class MessageRouter
{
    private readonly Settings _settings;
    private readonly PipeServer _pipe;
    private readonly TelegramService _telegram;
    private readonly SimConnectService _sim;

    public string? LastSenderCallsign { get; private set; }
    public bool Paused { get; set; }

    public MessageRouter(Settings settings, PipeServer pipe, TelegramService telegram, SimConnectService sim)
    {
        _settings = settings;
        _pipe = pipe;
        _telegram = telegram;
        _sim = sim;

        _pipe.MessageReceived += OnVPilotMessage;
        _telegram.IncomingText += OnTelegramText;
    }

    private async void OnVPilotMessage(IpcMessage msg)
    {
        try { await OnVPilotMessageCore(msg); }
        catch { /* never let an event handler bring down the app */ }
    }

    private async Task OnVPilotMessageCore(IpcMessage msg)
    {
        if (Paused || IsQuiet()) return;

        switch (msg)
        {
            case PrivateMessageEvent pm when _settings.ForwardPrivateMessages && !IsIgnored(pm.From):
                LastSenderCallsign = pm.From;
                await _telegram.SendAsync($"<b>✉️ {Esc(pm.From)}</b>\n{Esc(pm.Message)}\n\n<i>Reply: just type. Or .chat CALLSIGN msg</i>");
                break;

            case RadioMessageEvent rm when _settings.ForwardRadioMessages && !IsIgnored(rm.From):
                var freqs = string.Join(", ", rm.Frequencies.Select(FormatFreq));
                await _telegram.SendAsync($"<b>📻 {Esc(rm.From)}</b> on {freqs}\n{Esc(rm.Message)}");
                break;

            case BroadcastMessageEvent bm when _settings.ForwardBroadcastMessages && !IsIgnored(bm.From):
                await _telegram.SendAsync($"<b>📢 {Esc(bm.From)}</b> (broadcast)\n{Esc(bm.Message)}");
                break;

            case SelcalEvent sc when _settings.ForwardSelcal && !IsIgnored(sc.From):
                var selFreqs = string.Join(", ", sc.Frequencies.Select(FormatFreq));
                await _telegram.SendAsync($"<b>🔔 SELCAL</b> from {Esc(sc.From)} on {selFreqs}");
                break;

            case NetworkConnectedEvent nc when _settings.ForwardConnectionEvents:
                await _telegram.SendAsync($"<b>🟢 Connected</b> as {Esc(nc.Callsign)} ({Esc(nc.TypeCode)}{(nc.ObserverMode ? " · observer" : "")})");
                break;

            case NetworkDisconnectedEvent when _settings.ForwardConnectionEvents:
                await _telegram.SendAsync("<b>🔴 Disconnected</b> from VATSIM");
                break;

            case MetarReceivedEvent met:
                await _telegram.SendAsync($"<b>🌦️ METAR</b>\n<pre>{Esc(met.Metar)}</pre>");
                break;

            case AtisReceivedEvent atis when !IsIgnored(atis.From):
                var body = string.Join("\n", atis.Lines);
                await _telegram.SendAsync($"<b>📋 ATIS {Esc(atis.From)}</b>\n<pre>{Esc(body)}</pre>");
                break;
        }
    }

    private async void OnTelegramText(long chatId, string text)
    {
        try { await OnTelegramTextCore(text); }
        catch { /* never let an event handler bring down the app */ }
    }

    private async Task OnTelegramTextCore(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;

        if (text.StartsWith(".", StringComparison.Ordinal) || text.StartsWith("/", StringComparison.Ordinal))
        {
            await HandleCommand(text.Substring(1));
            return;
        }

        if (LastSenderCallsign is null)
        {
            await _telegram.SendAsync("No recent message to reply to. Use <code>/chat CALLSIGN message</code> or <code>.chat ...</code>");
            return;
        }

        await SendPm(LastSenderCallsign, text);
    }

    private async Task HandleCommand(string text)
    {
        var (cmd, rest) = SplitCmd(text);
        cmd = cmd.ToLowerInvariant();
        // Strip Telegram bot mention suffix: "/help@MyBot"
        var at = cmd.IndexOf('@');
        if (at > 0) cmd = cmd.Substring(0, at);

        switch (cmd)
        {
            case "metar":
            case "wx":
                if (string.IsNullOrWhiteSpace(rest)) { await _telegram.SendAsync("Usage: <code>/metar STATION</code>"); return; }
                if (!_pipe.IsConnected) { await _telegram.SendAsync("vPilot not connected. Open vPilot and try again."); return; }
                await _pipe.SendAsync(new RequestMetarCommand(rest.Trim().ToUpperInvariant()));
                await _telegram.SendAsync($"<i>↗ METAR requested for {Esc(rest.Trim().ToUpperInvariant())}</i>");
                break;

            case "atis":
                if (string.IsNullOrWhiteSpace(rest)) { await _telegram.SendAsync("Usage: <code>/atis CALLSIGN</code>"); return; }
                if (!_pipe.IsConnected) { await _telegram.SendAsync("vPilot not connected. Open vPilot and try again."); return; }
                await _pipe.SendAsync(new RequestAtisCommand(rest.Trim().ToUpperInvariant()));
                await _telegram.SendAsync($"<i>↗ ATIS requested from {Esc(rest.Trim().ToUpperInvariant())}</i>");
                break;

            case "chat":
            case "msg":
            case "to":
                await HandleChat(rest);
                break;

            case "radio":
                await HandleRadio(rest);
                break;

            case "ident":
                await _pipe.SendAsync(new SquawkIdentCommand());
                await _telegram.SendAsync("<i>↗ Squawk ident</i>");
                break;

            case "x":
            case "xpdr":
            case "xpndr":
            case "squawk":
                await HandleSquawk(rest);
                break;

            case "com1":
                await HandleCom(rest, com1: true);
                break;

            case "com2":
                await HandleCom(rest, com1: false);
                break;

            case "modec":
                var arg = rest.Trim().ToLowerInvariant();
                if (arg != "on" && arg != "off") { await _telegram.SendAsync("Usage: <code>/modec on|off</code>"); return; }
                await _pipe.SendAsync(new SetModeCCommand(arg == "on"));
                await _telegram.SendAsync($"<i>↗ Mode C {arg}</i>");
                break;

            case "dis":
            case "disconnect":
                await _pipe.SendAsync(new RequestDisconnectCommand());
                await _telegram.SendAsync("<i>↗ Disconnect requested</i>");
                break;

            case "ignore":
                if (string.IsNullOrWhiteSpace(rest)) { await _telegram.SendAsync("Usage: <code>/ignore CALLSIGN</code>"); return; }
                AddIgnore(rest.Trim().ToUpperInvariant());
                await _telegram.SendAsync($"<i>🔕 Ignoring {Esc(rest.Trim().ToUpperInvariant())}</i>");
                break;

            case "unignore":
                if (string.IsNullOrWhiteSpace(rest)) { await _telegram.SendAsync("Usage: <code>/unignore CALLSIGN</code>"); return; }
                RemoveIgnore(rest.Trim().ToUpperInvariant());
                await _telegram.SendAsync($"<i>🔔 Unignored {Esc(rest.Trim().ToUpperInvariant())}</i>");
                break;

            case "ignorelist":
                var list = _settings.IgnoredCallsigns;
                await _telegram.SendAsync(list.Count == 0
                    ? "<i>Ignore list is empty.</i>"
                    : "<b>Ignored:</b>\n" + string.Join("\n", list.Select(c => "• " + Esc(c))));
                break;

            case "help":
            case "?":
            case "start":
                await _telegram.SendAsync(HelpText());
                break;

            default:
                await _telegram.SendAsync($"Unknown command <code>/{Esc(cmd)}</code>. Send <code>/help</code>");
                break;
        }
    }

    private async Task HandleChat(string rest)
    {
        rest = rest.TrimStart();
        var space = rest.IndexOf(' ');
        if (space < 1) { await _telegram.SendAsync("Usage: <code>/chat CALLSIGN message</code>"); return; }
        var to = rest.Substring(0, space).ToUpperInvariant();
        var msg = rest.Substring(space + 1);
        await SendPm(to, msg);
    }

    private async Task HandleSquawk(string rest)
    {
        rest = rest.Trim();
        if (string.IsNullOrEmpty(rest))
        {
            await _pipe.SendAsync(new SquawkIdentCommand());
            await _telegram.SendAsync("<i>↗ Squawk ident</i>");
            return;
        }
        if (rest.Length != 4 || !rest.All(c => c >= '0' && c <= '7'))
        {
            await _telegram.SendAsync("Squawk must be 4 octal digits, e.g. <code>/squawk 1200</code>");
            return;
        }
        if (!_sim.IsConnected)
        {
            await _telegram.SendAsync("Not connected to MSFS — needed to set squawk code. Start MSFS first.");
            return;
        }
        uint bco = 0;
        foreach (var c in rest) bco = (bco << 4) | (uint)(c - '0');
        await _sim.SetTransponderBcoAsync(bco);
        await _telegram.SendAsync($"<i>↗ Squawk {Esc(rest)}</i>");
    }

    private async Task HandleCom(string rest, bool com1)
    {
        rest = rest.Trim();
        if (string.IsNullOrEmpty(rest))
        {
            await _telegram.SendAsync($"Usage: <code>/com{(com1 ? 1 : 2)} 118.700</code>");
            return;
        }
        if (!double.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz)
            && !double.TryParse(rest, NumberStyles.Float, CultureInfo.GetCultureInfo("de-DE"), out mhz))
        {
            await _telegram.SendAsync("Frequency must be like <code>118.700</code>");
            return;
        }
        if (mhz < 118.0 || mhz > 137.0)
        {
            await _telegram.SendAsync("Frequency out of VHF aviation range (118.000–136.975).");
            return;
        }
        if (!_sim.IsConnected)
        {
            await _telegram.SendAsync("Not connected to MSFS — needed to tune COM. Start MSFS first.");
            return;
        }
        var hz = (uint)Math.Round(mhz * 1_000_000);
        if (com1) await _sim.SetCom1HzAsync(hz); else await _sim.SetCom2HzAsync(hz);
        await _telegram.SendAsync($"<i>↗ COM{(com1 ? 1 : 2)} → {mhz.ToString("0.000", CultureInfo.InvariantCulture)}</i>");
    }

    private async Task HandleRadio(string msg)
    {
        msg = msg.Trim();
        if (string.IsNullOrEmpty(msg)) { await _telegram.SendAsync("Usage: <code>/radio message</code>"); return; }
        await _pipe.SendAsync(new SendRadioMessageCommand(msg));
        await _telegram.SendAsync($"<i>📻 → {Esc(msg)}</i>");
    }

    private async Task SendPm(string to, string msg)
    {
        await _pipe.SendAsync(new SendPrivateMessageCommand(to, msg));
        LastSenderCallsign = to;
        await _telegram.SendAsync($"<i>→ {Esc(to)}: {Esc(msg)}</i>");
    }

    private void AddIgnore(string callsign)
    {
        if (!_settings.IgnoredCallsigns.Contains(callsign, StringComparer.OrdinalIgnoreCase))
        {
            _settings.IgnoredCallsigns.Add(callsign);
            _settings.Save();
        }
    }

    private void RemoveIgnore(string callsign)
    {
        var removed = _settings.IgnoredCallsigns.RemoveAll(c => string.Equals(c, callsign, StringComparison.OrdinalIgnoreCase));
        if (removed > 0) _settings.Save();
    }

    private bool IsIgnored(string callsign)
        => _settings.IgnoredCallsigns.Any(c => string.Equals(c, callsign, StringComparison.OrdinalIgnoreCase));

    private static (string cmd, string rest) SplitCmd(string s)
    {
        var space = s.IndexOf(' ');
        if (space < 0) return (s, string.Empty);
        return (s.Substring(0, space), s.Substring(space + 1));
    }

    private static string HelpText() =>
        "<b>vatgram commands</b>\n" +
        "<i>Both <code>/cmd</code> and <code>.cmd</code> work. Tap the / button for the menu.</i>\n" +
        "\n<b>Reply</b>\n" +
        "• plain text → reply to last sender\n" +
        "• <code>/chat CALLSIGN msg</code> (alias <code>/msg</code>, <code>/to</code>)\n" +
        "• <code>/radio msg</code> → text on current TX freq\n" +
        "\n<b>Info</b>\n" +
        "• <code>/metar STATION</code> (alias <code>/wx</code>)\n" +
        "• <code>/atis CALLSIGN</code>\n" +
        "\n<b>Aircraft (vPilot)</b>\n" +
        "• <code>/ident</code> → squawk ident\n" +
        "• <code>/modec on|off</code> → mode C\n" +
        "• <code>/dis</code> → disconnect VATSIM\n" +
        "\n<b>Cockpit (MSFS via SimConnect)</b>\n" +
        "• <code>/com1 118.700</code>\n" +
        "• <code>/com2 121.500</code>\n" +
        "• <code>/squawk 1234</code> (alias <code>/x 1234</code>)\n" +
        "\n<b>Filtering</b>\n" +
        "• <code>/ignore CALLSIGN</code> / <code>/unignore CALLSIGN</code> / <code>/ignorelist</code>\n" +
        "\n<i>Cockpit commands need MSFS running (SimConnect).</i>";

    private static string FormatFreq(int f)
    {
        var s = f.ToString().PadLeft(5, '0');
        return $"1{s.Substring(0, 2)}.{s.Substring(2)}";
    }

    private static string Esc(string s) =>
        new StringBuilder(s).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").ToString();

    private bool IsQuiet()
    {
        if (!_settings.QuietHoursEnabled) return false;
        var s = _settings.QuietHoursStart;
        var e = _settings.QuietHoursEnd;
        if (s == e) return false; // empty / undefined window
        var nowMin = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
        return s < e ? (nowMin >= s && nowMin < e) : (nowMin >= s || nowMin < e);
    }
}
