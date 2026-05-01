using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Telegram.Bot;
using Vatgram.Tray.Interop;
using Vatgram.Tray.Services;

namespace Vatgram.Tray.Windows;

public partial class SettingsWindow : Window
{
    private readonly Settings _settings;
    private int _selected = -1;

    private TextBox _tokenBox = null!;
    private TextBlock _chatLabel = null!;
    private ToggleButton _tPm = null!, _tRadio = null!, _tBroadcast = null!, _tSelcal = null!, _tConn = null!;
    private ToggleButton _tQuiet = null!, _tStartup = null!;
    private ComboBox? _quietStart, _quietEnd;

    public SettingsWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Mica.Apply(this);
        LogoImage.Source = TrayIcons.Logo(56);
        _settings = App.Current.SettingsModel;

        BuildNav();
        ShowSection(0);
        FooterStatus.Text = $"v{ThisVersion()}";
    }

    private void BuildNav()
    {
        var items = new[] { "Telegram", "Forwarding", "Schedule", "Advanced", "About" };
        for (var i = 0; i < items.Length; i++)
        {
            var idx = i;
            var btn = new Button
            {
                Style = (Style)FindResource("ButtonGhost"),
                Content = new TextBlock { Text = items[i], HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center },
                Margin = new Thickness(0, 0, 0, 4),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Tag = idx
            };
            btn.Click += (_, _) => ShowSection(idx);
            NavList.Children.Add(btn);
        }
    }

    private void ShowSection(int idx)
    {
        if (_selected == idx) return;
        _selected = idx;

        for (var i = 0; i < NavList.Children.Count; i++)
        {
            if (NavList.Children[i] is Button b)
            {
                var active = i == idx;
                b.Background = active ? new SolidColorBrush(Color.FromArgb(40, 0x38, 0xA3, 0xF0)) : Brushes.Transparent;
                ((TextBlock)b.Content).FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }

        SectionHost.Children.Clear();
        var content = idx switch
        {
            0 => BuildTelegram(),
            1 => BuildForwarding(),
            2 => BuildSchedule(),
            3 => BuildAdvanced(),
            _ => BuildAbout()
        };
        SectionHost.Children.Add(content);
    }

    private FrameworkElement BuildTelegram()
    {
        var sp = new StackPanel();
        sp.Children.Add(NewTitle("Telegram"));
        sp.Children.Add(NewCaption("Bot token from @BotFather"));
        var tokenRow = new Grid();
        tokenRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tokenRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _tokenBox = new TextBox { Text = _settings.TelegramBotToken ?? "", Margin = new Thickness(0, 0, 8, 0) };
        var testBtn = new Button { Content = "Test token", Style = (Style)FindResource("ButtonSecondary"), Width = 120 };
        testBtn.Click += async (_, _) => await TestTokenAsync();
        Grid.SetColumn(_tokenBox, 0); Grid.SetColumn(testBtn, 1);
        tokenRow.Children.Add(_tokenBox); tokenRow.Children.Add(testBtn);
        sp.Children.Add(tokenRow);

        sp.Children.Add(NewCaption("Chat binding"));
        _chatLabel = NewBody(_settings.TelegramChatId is { } id ? $"Chat bound  ·  ID {id}" : "Not bound. Send /start to your bot to bind.");
        _chatLabel.Foreground = (Brush)FindResource("TextSecondary");
        sp.Children.Add(_chatLabel);
        return sp;
    }

    private FrameworkElement BuildForwarding()
    {
        var sp = new StackPanel();
        sp.Children.Add(NewTitle("Forwarding"));
        _tPm = AddToggleRow(sp, "Private messages", "Forward incoming PMs to Telegram", _settings.ForwardPrivateMessages);
        _tRadio = AddToggleRow(sp, "Radio messages", "Text radio messages on tuned frequencies", _settings.ForwardRadioMessages);
        _tBroadcast = AddToggleRow(sp, "Broadcast messages", "Server-wide broadcasts and CTAFs", _settings.ForwardBroadcastMessages);
        _tSelcal = AddToggleRow(sp, "SELCAL alerts", "When a controller pings your SELCAL", _settings.ForwardSelcal);
        _tConn = AddToggleRow(sp, "Connect / disconnect", "Network connect and disconnect events", _settings.ForwardConnectionEvents);
        return sp;
    }

    private FrameworkElement BuildSchedule()
    {
        var sp = new StackPanel();
        sp.Children.Add(NewTitle("Schedule"));
        _tQuiet = AddToggleRow(sp, "Quiet hours", "Suppress all forwards during the time window below", _settings.QuietHoursEnabled);

        var row = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _quietStart = TimeBox(_settings.QuietHoursStart);
        var dash = new TextBlock { Text = "→", Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("TextSecondary") };
        _quietEnd = TimeBox(_settings.QuietHoursEnd);
        var unit = new TextBlock { Text = "  (24h, 30 min steps)", Style = (Style)FindResource("TextSmall"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_quietStart, 0); Grid.SetColumn(dash, 1); Grid.SetColumn(_quietEnd, 2); Grid.SetColumn(unit, 3);
        row.Children.Add(_quietStart); row.Children.Add(dash); row.Children.Add(_quietEnd); row.Children.Add(unit);
        sp.Children.Add(row);
        return sp;
    }

    private FrameworkElement BuildAdvanced()
    {
        var sp = new StackPanel();
        sp.Children.Add(NewTitle("Advanced"));
        _tStartup = AddToggleRow(sp, "Start with Windows", "Launch vatGram automatically on logon", _settings.StartWithWindows);
        return sp;
    }

    private FrameworkElement BuildAbout()
    {
        var sp = new StackPanel();
        sp.Children.Add(NewTitle("About"));
        sp.Children.Add(NewBody($"vatGram v{ThisVersion()}\nvPilot ↔ Telegram bridge for VATSIM"));

        var divider = new TextBlock { Text = "SYSTEM STATUS", Style = (Style)FindResource("TextSmall"), Foreground = (Brush)FindResource("TextTertiary"), Margin = new Thickness(0, 24, 0, 12) };
        sp.Children.Add(divider);
        sp.Children.Add(BuildStatusRow("Telegram", App.Current.Telegram.IsRunning));
        sp.Children.Add(BuildStatusRow("vPilot plugin", App.Current.Pipe.IsConnected));
        sp.Children.Add(BuildStatusRow("MSFS (SimConnect)", App.Current.Sim.IsConnected));

        var divider2 = new TextBlock { Text = "STORAGE", Style = (Style)FindResource("TextSmall"), Foreground = (Brush)FindResource("TextTertiary"), Margin = new Thickness(0, 24, 0, 12) };
        sp.Children.Add(divider2);
        var btn = new Button { Content = "Open settings folder", Style = (Style)FindResource("ButtonSecondary"), Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        btn.Click += (_, _) => System.Diagnostics.Process.Start("explorer.exe", Settings.AppDataDirectory);
        sp.Children.Add(btn);

        // Made-with footer
        var credit = new TextBlock
        {
            Style = (Style)FindResource("TextSmall"),
            Foreground = (Brush)FindResource("TextTertiary"),
            Margin = new Thickness(0, 32, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        credit.Inlines.Add(new System.Windows.Documents.Run("Made with ♥ by "));
        var link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("flywithpascal.com"))
        {
            NavigateUri = new Uri("https://flywithpascal.com"),
            Foreground = (Brush)FindResource("AccentPrimary"),
            TextDecorations = null
        };
        link.RequestNavigate += (_, e) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true }); } catch { }
            e.Handled = true;
        };
        credit.Inlines.Add(link);
        credit.Inlines.Add(new System.Windows.Documents.Run(" in Switzerland 🇨🇭"));
        sp.Children.Add(credit);
        return sp;
    }

    private FrameworkElement BuildStatusRow(string name, bool ok)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var dot = new System.Windows.Shapes.Ellipse { Width = 10, Height = 10, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Fill = (Brush)FindResource(ok ? "StatusActive" : "StatusError") };
        var lbl = new TextBlock { Text = name, Style = (Style)FindResource("TextBody"), VerticalAlignment = VerticalAlignment.Center };
        var state = new TextBlock { Text = ok ? "Connected" : "Offline", Style = (Style)FindResource("TextSmall"), Foreground = (Brush)FindResource(ok ? "StatusActive" : "TextSecondary"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(dot, 0); Grid.SetColumn(lbl, 1); Grid.SetColumn(state, 2);
        grid.Children.Add(dot); grid.Children.Add(lbl); grid.Children.Add(state);
        return grid;
    }

    private TextBlock NewTitle(string text) => new()
    {
        Text = text,
        Style = (Style)FindResource("TextDisplay"),
        Margin = new Thickness(0, 0, 0, 24)
    };

    private TextBlock NewCaption(string text) => new()
    {
        Text = text,
        Style = (Style)FindResource("TextSmall"),
        Margin = new Thickness(0, 16, 0, 8)
    };

    private TextBlock NewBody(string text) => new()
    {
        Text = text,
        Style = (Style)FindResource("TextBody"),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 8)
    };

    private ToggleButton AddToggleRow(Panel parent, string title, string hint, bool value)
    {
        var grid = new Grid { Margin = new Thickness(0, 8, 0, 8), Cursor = Cursors.Hand };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stack = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        stack.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("TextBody"), FontWeight = FontWeights.SemiBold });
        if (!string.IsNullOrEmpty(hint))
            stack.Children.Add(new TextBlock { Text = hint, Style = (Style)FindResource("TextSmall"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });

        var toggle = new ToggleButton { Style = (Style)FindResource("ToggleSwitch"), IsChecked = value, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(stack, 0); Grid.SetColumn(toggle, 1);
        grid.Children.Add(stack); grid.Children.Add(toggle);
        grid.MouseLeftButtonUp += (_, _) => toggle.IsChecked = !(toggle.IsChecked ?? false);

        parent.Children.Add(grid);
        return toggle;
    }

    private ComboBox TimeBox(int valueMinutes)
    {
        var cb = new ComboBox { Width = 96 };
        for (var m = 0; m < 24 * 60; m += 30)
            cb.Items.Add($"{m / 60:D2}:{m % 60:D2}");
        // Snap to nearest valid index
        var idx = Math.Clamp(valueMinutes / 30, 0, cb.Items.Count - 1);
        cb.SelectedIndex = idx;
        return cb;
    }

    private async Task TestTokenAsync()
    {
        var token = _tokenBox.Text.Trim();
        if (string.IsNullOrEmpty(token)) { MessageBox.Show(this, "Enter a token first."); return; }
        try
        {
            var bot = new Telegram.Bot.TelegramBotClient(token);
            var me = await bot.GetMe();
            MessageBox.Show(this, $"OK — connected as @{me.Username}", "Telegram", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, "Failed: " + ex.Message, "Telegram", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        // Persist whatever fields are bound (some sections may not have been visited)
        if (_tokenBox != null) _settings.TelegramBotToken = _tokenBox.Text.Trim();
        if (_tPm != null) _settings.ForwardPrivateMessages = _tPm.IsChecked ?? true;
        if (_tRadio != null) _settings.ForwardRadioMessages = _tRadio.IsChecked ?? false;
        if (_tBroadcast != null) _settings.ForwardBroadcastMessages = _tBroadcast.IsChecked ?? true;
        if (_tSelcal != null) _settings.ForwardSelcal = _tSelcal.IsChecked ?? true;
        if (_tConn != null) _settings.ForwardConnectionEvents = _tConn.IsChecked ?? true;
        if (_tQuiet != null) _settings.QuietHoursEnabled = _tQuiet.IsChecked ?? false;
        if (_quietStart != null) _settings.QuietHoursStart = _quietStart.SelectedIndex * 30;
        if (_quietEnd != null) _settings.QuietHoursEnd = _quietEnd.SelectedIndex * 30;
        if (_tStartup != null) { _settings.StartWithWindows = _tStartup.IsChecked ?? false; StartupRegistration.Apply(_settings.StartWithWindows); }
        try { _settings.Save(); }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to save settings:\n{ex.Message}", "Save error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        _ = App.Current.StartTelegramAsync();
        Close();
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private static string ThisVersion() => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
}
