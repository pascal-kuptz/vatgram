using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Vatgram.Tray.Services;

public sealed class TelegramService : IDisposable
{
    private TelegramBotClient? _bot;
    private CancellationTokenSource? _cts;
    private long? _allowedChatId;

    public event Action<bool>? ConnectionChanged;
    public event Action<long, string>? IncomingText;
    public event Action<User>? FirstStartReceived;
    public event Action<string>? ErrorOccurred;

    public bool IsRunning => _bot != null;

    public async Task<User> StartAsync(string token, long? allowedChatId)
    {
        Stop();
        _allowedChatId = allowedChatId;
        _bot = new TelegramBotClient(token);
        _cts = new CancellationTokenSource();

        var me = await _bot.GetMe(_cts.Token);

        try { await _bot.SetMyCommands(BotMenuCommands, cancellationToken: _cts.Token); }
        catch { /* not critical */ }

        _bot.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: new ReceiverOptions { AllowedUpdates = [UpdateType.Message] },
            cancellationToken: _cts.Token);

        ConnectionChanged?.Invoke(true);
        return me;
    }

    private static readonly BotCommand[] BotMenuCommands =
    {
        new() { Command = "help",       Description = "Show all commands" },
        new() { Command = "metar",      Description = "METAR for station — /metar EDDF" },
        new() { Command = "atis",       Description = "ATIS for controller — /atis EDDF_TWR" },
        new() { Command = "chat",       Description = "PM a callsign — /chat EDDF_TWR hello" },
        new() { Command = "radio",      Description = "Send text on TX freq — /radio hello" },
        new() { Command = "com1",       Description = "Tune COM1 — /com1 118.700" },
        new() { Command = "com2",       Description = "Tune COM2 — /com2 121.500" },
        new() { Command = "squawk",     Description = "Set squawk — /squawk 1200" },
        new() { Command = "ident",      Description = "Squawk ident" },
        new() { Command = "modec",      Description = "Mode C on/off — /modec on" },
        new() { Command = "dis",        Description = "Disconnect from VATSIM" },
        new() { Command = "ignore",     Description = "Mute a callsign — /ignore EDDF_TWR" },
        new() { Command = "unignore",   Description = "Unmute a callsign — /unignore EDDF_TWR" },
        new() { Command = "ignorelist", Description = "Show ignored callsigns" },
    };

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        _cts?.Dispose();
        _cts = null;
        _bot = null;
        ConnectionChanged?.Invoke(false);
    }

    public void UpdateAllowedChatId(long? chatId) => _allowedChatId = chatId;

    public async Task SendAsync(string text)
    {
        if (_bot is null || _allowedChatId is null) return;
        try
        {
            await _bot.SendMessage(
                chatId: _allowedChatId.Value,
                text: text,
                parseMode: ParseMode.Html,
                cancellationToken: _cts?.Token ?? CancellationToken.None);
        }
        catch { /* swallow; offline etc. */ }
    }

    private Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is not { } msg || msg.Text is null) return Task.CompletedTask;
        var chatId = msg.Chat.Id;

        if (_allowedChatId is null && msg.Text.StartsWith("/start"))
        {
            FirstStartReceived?.Invoke(msg.From!);
            return Task.CompletedTask;
        }

        if (_allowedChatId is null || chatId != _allowedChatId) return Task.CompletedTask;

        IncomingText?.Invoke(chatId, msg.Text);
        return Task.CompletedTask;
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, HandleErrorSource source, CancellationToken ct)
    {
        // Polling/network errors come through here. Log them so the user can diagnose
        // ("offline", "401 unauthorized", etc.) and surface to the tray status.
        try
        {
            var path = System.IO.Path.Combine(Settings.LogDirectory, $"telegram-{DateTime.UtcNow:yyyyMMdd}.log");
            System.IO.File.AppendAllText(path, $"{DateTime.UtcNow:o}  {source}  {ex}\n");
        }
        catch { }
        try { ErrorOccurred?.Invoke($"{source}: {ex.Message}"); } catch { }
        return Task.CompletedTask;
    }

    public void Dispose() => Stop();
}
