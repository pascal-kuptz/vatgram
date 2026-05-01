using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Vatgram.Tray.Interop;

namespace Vatgram.Tray.Windows;

public partial class OnboardingWindow : Window
{
    private int _stepIndex;
    private Telegram.Bot.Types.User? _detectedUser;

    public OnboardingWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Mica.Apply(this);
        LogoImage.Source = TrayIcons.Logo(80);
        App.Current.Telegram.FirstStartReceived += OnFirstStart;
        Closed += (_, _) => App.Current.Telegram.FirstStartReceived -= OnFirstStart;
        Goto(0);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnBack(object sender, RoutedEventArgs e) => Goto(_stepIndex - 1);

    private async void OnNext(object sender, RoutedEventArgs e)
    {
        if (_stepIndex == 0) { Goto(1); return; }
        if (_stepIndex == 1)
        {
            var token = TokenBox.Password.Trim();
            if (string.IsNullOrEmpty(token)) { MessageBox.Show(this, "Paste the token from BotFather first."); return; }
            NextBtn.IsEnabled = false;
            try
            {
                var me = await App.Current.Telegram.StartAsync(token, allowedChatId: null);
                App.Current.SettingsModel.TelegramBotToken = token;
                App.Current.SettingsModel.Save();
                Step3Status.Text = $"✅ Bot @{me.Username} is online.\nNow open Telegram and send /start to your bot.";
                Step3Status.Foreground = (Brush)FindResource("StatusActive");
                Goto(2);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Token rejected by Telegram: " + ex.Message, "Setup", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { NextBtn.IsEnabled = true; }
            return;
        }
        if (_stepIndex == 2)
        {
            if (_detectedUser is null) { MessageBox.Show(this, "Send /start to your bot in Telegram first."); return; }
            App.Current.SettingsModel.TelegramChatId = _detectedUser.Id;
            App.Current.SettingsModel.Save();
            App.Current.Telegram.UpdateAllowedChatId(_detectedUser.Id);
            await App.Current.Telegram.SendAsync("✅ vatGram setup complete. You'll receive vPilot messages here.");
            App.Current.RefreshTrayIcon();
            Close();
        }
    }

    private void OnFirstStart(Telegram.Bot.Types.User user)
    {
        Dispatcher.Invoke(() =>
        {
            _detectedUser = user;
            Step3Status.Text = $"✅ Detected: {user.FirstName} (@{user.Username})\nClick Finish to bind.";
            Step3Status.Foreground = (Brush)FindResource("StatusActive");
            NextBtn.Content = "Finish";
        });
    }

    private void Goto(int index)
    {
        var newIndex = Math.Max(0, Math.Min(2, index));
        if (newIndex == _stepIndex && IsLoaded) return;
        var oldIndex = _stepIndex;
        _stepIndex = newIndex;

        BackBtn.IsEnabled = _stepIndex > 0;
        StepIndicator.Text = $"Step {_stepIndex + 1} of 3";
        NextBtn.Content = _stepIndex == 2 ? "Finish" : "Next";

        Dot1.Fill = (Brush)FindResource(_stepIndex >= 0 ? "AccentPrimary" : "BorderStrong");
        Dot2.Fill = (Brush)FindResource(_stepIndex >= 1 ? "AccentPrimary" : "BorderStrong");
        Dot3.Fill = (Brush)FindResource(_stepIndex >= 2 ? "AccentPrimary" : "BorderStrong");

        var oldPanel = GetPanel(oldIndex);
        var newPanel = GetPanel(_stepIndex);
        if (oldPanel == newPanel) { newPanel.Visibility = Visibility.Visible; return; }

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var direction = _stepIndex > oldIndex ? 1 : -1;

        oldPanel.BeginAnimation(OpacityProperty, new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(160), EasingFunction = ease });
        ((TranslateTransform)oldPanel.RenderTransform).BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation { To = -24 * direction, Duration = TimeSpan.FromMilliseconds(160), EasingFunction = ease });

        newPanel.Opacity = 0;
        ((TranslateTransform)newPanel.RenderTransform).X = 24 * direction;
        newPanel.Visibility = Visibility.Visible;
        newPanel.BeginAnimation(OpacityProperty,
            new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(240), BeginTime = TimeSpan.FromMilliseconds(80), EasingFunction = ease });
        ((TranslateTransform)newPanel.RenderTransform).BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation { From = 24 * direction, To = 0, Duration = TimeSpan.FromMilliseconds(240), BeginTime = TimeSpan.FromMilliseconds(80), EasingFunction = ease });

        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        t.Tick += (_, _) => { t.Stop(); oldPanel.Visibility = Visibility.Collapsed; };
        t.Start();
    }

    private FrameworkElement GetPanel(int index) => index switch { 0 => Step1, 1 => Step2, _ => Step3 };
}
