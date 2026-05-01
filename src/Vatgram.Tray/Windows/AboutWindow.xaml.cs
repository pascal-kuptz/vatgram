using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using Vatgram.Tray.Interop;

namespace Vatgram.Tray.Windows;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Mica.Apply(this);
        LogoImage.Source = TrayIcons.Logo(80);
        VersionLabel.Text = $"version {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
        StatusList.Children.Add(BuildRow("Telegram", App.Current.Telegram.IsRunning));
        StatusList.Children.Add(BuildRow("vPilot plugin", App.Current.Pipe.IsConnected));
        StatusList.Children.Add(BuildRow("MSFS (SimConnect)", App.Current.Sim.IsConnected));
    }

    private FrameworkElement BuildRow(string name, bool ok)
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

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnLinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true }); }
        catch { /* swallow — best effort browser launch */ }
        e.Handled = true;
    }
}
