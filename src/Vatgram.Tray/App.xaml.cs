using System.IO;
using System.Threading;
using System.Windows;
using H.NotifyIcon;
using Vatgram.Tray.Interop;
using Vatgram.Tray.Services;
using Vatgram.Tray.Windows;

namespace Vatgram.Tray;

public partial class App : Application
{
    private static Mutex? _singleInstance;

    public Settings SettingsModel { get; } = Settings.Load();
    public PipeServer Pipe { get; } = new();
    public TelegramService Telegram { get; } = new();
    public SimConnectService Sim { get; } = new();
    public MessageRouter Router { get; private set; } = null!;
    public bool Paused { get; set; }

    private TaskbarIcon? _trayIcon;
    private TrayPopup? _trayPopup;
    private volatile bool _shuttingDown;
    private SettingsWindow? _settingsWindow;
    private OnboardingWindow? _onboardingWindow;
    private AboutWindow? _aboutWindow;

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) => { LogCrash(args.Exception); args.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash(args.ExceptionObject as Exception);

        try
        {
            _singleInstance = new Mutex(initiallyOwned: true, name: @"Local\Vatgram.SingleInstance", out var createdNew);
            if (!createdNew) { _singleInstance.Dispose(); _singleInstance = null; Shutdown(); return; }
        }
        catch (AbandonedMutexException)
        {
            // Previous instance crashed without releasing — we still own it now.
        }

        Router = new MessageRouter(SettingsModel, Pipe, Telegram, Sim);

        _trayPopup = new TrayPopup();
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "vatGram",
            Icon = TrayIcons.Brand(),
            ContextMenu = null,
            TrayPopup = _trayPopup,
            PopupActivation = H.NotifyIcon.Core.PopupActivationMode.LeftClick,
            NoLeftClickDelay = true,
        };
        _trayIcon.ForceCreate();

        Telegram.FirstStartReceived += OnFirstStart;
        Pipe.ConnectionChanged += _ => Dispatcher.BeginInvoke(new Action(RefreshTrayIcon));
        Telegram.ConnectionChanged += _ => Dispatcher.BeginInvoke(new Action(RefreshTrayIcon));
        Telegram.ErrorOccurred += msg => Dispatcher.BeginInvoke(new Action(() => ShowBalloon("Telegram error", msg)));
        Sim.ConnectionChanged += _ => Dispatcher.BeginInvoke(new Action(RefreshTrayIcon));

        Pipe.Start();
        Sim.Start();

        if (SettingsModel.IsConfigured)
            _ = StartTelegramAsync();
        else
            Dispatcher.BeginInvoke(new Action(OpenOnboarding));

        RefreshTrayIcon();
    }

    public async Task StartTelegramAsync()
    {
        if (string.IsNullOrWhiteSpace(SettingsModel.TelegramBotToken)) return;
        try { await Telegram.StartAsync(SettingsModel.TelegramBotToken!, SettingsModel.TelegramChatId); }
        catch (Exception ex) { ShowBalloon("Telegram error", ex.Message); }
        _ = Dispatcher.BeginInvoke(new Action(RefreshTrayIcon));
    }

    private void OnFirstStart(Telegram.Bot.Types.User user)
    {
        // If the onboarding wizard is open, it owns the binding flow — don't double-prompt.
        if (_onboardingWindow != null) return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            var owner = (Window?)_settingsWindow ?? _aboutWindow;
            var result = owner != null
                ? MessageBox.Show(owner,
                    $"Telegram user '{user.FirstName}' (@{user.Username}) sent /start.\n\nBind this chat as your destination?",
                    "Bind Telegram chat", MessageBoxButton.YesNo, MessageBoxImage.Question)
                : MessageBox.Show(
                    $"Telegram user '{user.FirstName}' (@{user.Username}) sent /start.\n\nBind this chat as your destination?",
                    "Bind Telegram chat", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                SettingsModel.TelegramChatId = user.Id;
                try { SettingsModel.Save(); } catch { }
                Telegram.UpdateAllowedChatId(user.Id);
                _ = Telegram.SendAsync("✅ Bound. You'll receive vPilot messages here.");
                RefreshTrayIcon();
            }
        }));
    }

    public void OpenSettings()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        _settingsWindow.Activate();
    }

    public void OpenOnboarding()
    {
        if (_onboardingWindow == null || !_onboardingWindow.IsLoaded)
        {
            _onboardingWindow = new OnboardingWindow();
            _onboardingWindow.Closed += (_, _) => _onboardingWindow = null;
            _onboardingWindow.Show();
        }
        _onboardingWindow.Activate();
    }

    public void OpenAbout()
    {
        if (_aboutWindow == null || !_aboutWindow.IsLoaded)
        {
            _aboutWindow = new AboutWindow();
            _aboutWindow.Closed += (_, _) => _aboutWindow = null;
            _aboutWindow.Show();
        }
        _aboutWindow.Activate();
    }

    public void OpenLogs()
    {
        try { System.Diagnostics.Process.Start("explorer.exe", Settings.LogDirectory); } catch { }
    }

    public void TogglePaused()
    {
        Paused = !Paused;
        Router.Paused = Paused;
        RefreshTrayIcon();
    }

    public void ExitApp()
    {
        // Mark shutdown FIRST so any RefreshTrayIcon already queued on the dispatcher
        // (from in-flight ConnectionChanged events) short-circuits before touching
        // a disposed TaskbarIcon.
        _shuttingDown = true;

        // Stop sources before consumers: pipe + sim push events into the router which
        // may try to talk to telegram. Drain inbound first, then disconnect telegram.
        try { Pipe.Dispose(); } catch { }
        try { Sim.Dispose(); } catch { }
        try { Telegram.Dispose(); } catch { }
        try { _trayIcon?.Dispose(); } catch { }
        _trayIcon = null;
        try { _singleInstance?.ReleaseMutex(); } catch { }
        try { _singleInstance?.Dispose(); } catch { }
        _singleInstance = null;
        Shutdown();
    }

    public void RefreshTrayIcon()
    {
        if (_shuttingDown || _trayIcon == null) return;
        try
        {
            var anyOk = Telegram.IsRunning && Pipe.IsConnected;
            var newIcon = TrayIcons.Brand(anyOk);
            var oldIcon = _trayIcon.Icon;
            _trayIcon.Icon = newIcon;
            try { oldIcon?.Dispose(); } catch { }
            var tg = Telegram.IsRunning ? "✓" : "✗";
            var vp = Pipe.IsConnected ? "✓" : "✗";
            var sc = Sim.IsConnected ? "✓" : "✗";
            var paused = Paused ? " · paused" : "";
            _trayIcon.ToolTipText = $"vatGram · TG {tg} · vPilot {vp} · MSFS {sc}{paused}";
        }
        catch (ObjectDisposedException) { /* race with shutdown — ignore */ }
    }

    private void ShowBalloon(string title, string text)
    {
        try { _trayIcon?.ShowNotification(title, text); } catch { }
    }

    private static void LogCrash(Exception? ex)
    {
        try
        {
            var dir = Settings.LogDirectory;
            var path = Path.Combine(dir, $"crash-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(path, ex?.ToString() ?? "Unknown exception");
        }
        catch { }
    }
}
