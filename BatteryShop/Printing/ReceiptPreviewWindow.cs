using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BatteryShop.Helpers;

namespace BatteryShop.Printing;

/// <summary>On-screen receipt preview with Print / Close buttons (built in code, no XAML).</summary>
public class ReceiptPreviewWindow : Window
{
    public ReceiptPreviewWindow(Func<FrameworkElement> build, Action onPrinted)
    {
        Title = "Receipt preview";
        Width = 380;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current.MainWindow;
        Background = new SolidColorBrush(Color.FromRgb(0xE6, 0xE9, 0xED));

        var paper = new Border
        {
            Background = Brushes.White, Padding = new Thickness(10), Margin = new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Center, Child = build(),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 1, Opacity = 0.25 }
        };

        var print = new Button { Content = "Print", IsDefault = true, MinWidth = 100, Style = (Style)Application.Current.FindResource("PrimaryButton") };
        var close = new Button { Content = "Close", IsCancel = true, MinWidth = 100, Margin = new Thickness(0) };
        print.Click += (_, _) =>
        {
            if (UI.Run(() => { if (ReceiptPrinter.Print(build)) { onPrinted?.Invoke(); Close(); } })) { }
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
        buttons.Children.Add(print);
        buttons.Children.Add(close);

        var dock = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        dock.Children.Add(buttons);
        dock.Children.Add(new ScrollViewer { Content = paper, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = dock;
    }
}
