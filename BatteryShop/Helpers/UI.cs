using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using BatteryShop.Data;

namespace BatteryShop.Helpers;

/// <summary>Small helpers used by all screens: messages, number parsing, prompts.</summary>
public static class UI
{
    public const string AppTitle = "Ayaz Battery & Solar Service";

    public static void Error(Exception ex)
    {
        if (ex is AppException)
            MessageBox.Show(ex.Message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        else
            MessageBox.Show("Something went wrong:\n\n" + ex.Message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static void Info(string message)
        => MessageBox.Show(message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);

    public static bool Confirm(string message)
        => MessageBox.Show(message, AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    /// <summary>Runs an action and shows any error. Returns true if it succeeded.</summary>
    public static bool Run(Action action)
    {
        try { action(); return true; }
        catch (Exception ex) { Error(ex); return false; }
    }

    public static string Rs(long amount) => "Rs. " + amount.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>Parses a rupee amount like "18,500". Empty = 0. Throws a friendly error if invalid.</summary>
    public static long Money(string text, string fieldName = "Amount")
    {
        var t = (text ?? "").Replace(",", "").Replace("Rs.", "").Replace("Rs", "").Trim();
        if (t.Length == 0) return 0;
        if (long.TryParse(t, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v)) return v;
        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return (long)Math.Round(d);
        throw new AppException($"{fieldName}: '{text}' is not a valid number.");
    }

    /// <summary>A required whole number that must be greater than 0 (cost, quantity, price...).</summary>
    public static long Positive(string text, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new AppException($"{fieldName} is required.");
        var v = Money(text, fieldName);
        if (v <= 0) throw new AppException($"{fieldName} must be greater than 0.");
        return v;
    }

    /// <summary>A whole number that may be 0 but not negative (discount, paid amount...). Empty = 0.</summary>
    public static long NonNegative(string text, string fieldName)
    {
        var v = Money(text, fieldName);
        if (v < 0) throw new AppException($"{fieldName} cannot be negative.");
        return v;
    }

    public static long MoneyOrZero(string text)
    {
        try { return Money(text); } catch { return 0; }
    }

    public static double? Decimal(string text, string fieldName = "Value")
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) return null;
        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
        throw new AppException($"{fieldName}: '{text}' is not a valid number.");
    }

    public static string FormatDate(string dbDate)
    {
        if (DateTime.TryParse(dbDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.ToString("dd-MMM-yyyy");
        return dbDate;
    }

    public static string FormatDateTime(string dbDate)
    {
        if (DateTime.TryParse(dbDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.ToString("dd-MMM-yyyy hh:mm tt");
        return dbDate;
    }

    /// <summary>Asks the user to type something (e.g. a reason). Returns null if cancelled.</summary>
    public static string Prompt(string title, string message, string defaultValue = "")
    {
        var win = new Window
        {
            Title = title, Width = 420, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Application.Current.MainWindow
        };
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        var box = new TextBox { Text = defaultValue };
        panel.Children.Add(box);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Style = (Style)Application.Current.FindResource("PrimaryButton") };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Margin = new Thickness(0) };
        ok.Click += (_, _) => win.DialogResult = true;
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        win.Content = panel;
        win.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        return win.ShowDialog() == true ? box.Text.Trim() : null;
    }
}
