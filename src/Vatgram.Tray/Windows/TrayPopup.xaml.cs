using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vatgram.Tray.Interop;

namespace Vatgram.Tray.Windows;

public partial class TrayPopup : UserControl
{
    private static readonly Brush ActiveBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xAA));
    private static readonly Brush IdleBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x6B, 0x73));
    private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x54, 0x70));

    public TrayPopup()
    {
        InitializeComponent();
        ActiveBrush.Freeze();
        IdleBrush.Freeze();
        ErrorBrush.Freeze();
        LogoImage.Source = TrayIcons.Logo(56);
        Loaded += (_, _) => Refresh();
        IsVisibleChanged += (_, _) => { if (IsVisible) Refresh(); };
    }

    public void Refresh()
    {
        var app = App.Current;
        SetState(DotTelegram, StateTelegram, app.Telegram.IsRunning, "Connected", "Offline");
        SetState(DotVPilot, StateVPilot, app.Pipe.IsConnected, "Connected", "No plugin");
        SetState(DotSim, StateSim, app.Sim.IsConnected, "Ready", "Not running");
        BtnPause.Content = app.Paused ? "Resume notifications" : "Pause notifications";
    }

    private static void SetState(System.Windows.Shapes.Ellipse dot, TextBlock label, bool ok, string okText, string offText)
    {
        dot.Fill = ok ? ActiveBrush : ErrorBrush;
        label.Text = ok ? okText : offText;
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) { App.Current.OpenSettings(); ClosePopup(); }
    private void OnSetupClick(object sender, RoutedEventArgs e) { App.Current.OpenOnboarding(); ClosePopup(); }
    private void OnLogsClick(object sender, RoutedEventArgs e) { App.Current.OpenLogs(); ClosePopup(); }
    private void OnAboutClick(object sender, RoutedEventArgs e) { App.Current.OpenAbout(); ClosePopup(); }
    private void OnPauseClick(object sender, RoutedEventArgs e) { App.Current.TogglePaused(); Refresh(); }
    private void OnExitClick(object sender, RoutedEventArgs e) => App.Current.ExitApp();

    private void ClosePopup()
    {
        // H.NotifyIcon's TrayPopup is hosted in a popup window; closing the parent dismisses it.
        if (Parent is FrameworkElement fe && fe.Parent is System.Windows.Controls.Primitives.Popup p) p.IsOpen = false;
        else if (Window.GetWindow(this) is { } w) w.Hide();
    }
}
