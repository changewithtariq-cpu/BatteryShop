using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BatteryShop.Helpers;

/// <summary>
/// Restricts what can be typed or pasted into a TextBox.
/// Usage in XAML:  h:NumericInput.Mode="Integer"   (also "Decimal" or "Phone")
///   Integer : digits only (commas allowed for 18,000). No minus, no dot.
///   Digits  : digits only, no commas (used in grid cells).
///   Decimal : digits and one dot (e.g. weight 14.5). No minus.
///   Phone   : digits, +, -, and spaces.
/// </summary>
public static class NumericInput
{
    public static readonly DependencyProperty ModeProperty = DependencyProperty.RegisterAttached(
        "Mode", typeof(string), typeof(NumericInput), new PropertyMetadata(null, OnModeChanged));

    public static string GetMode(DependencyObject d) => (string)d.GetValue(ModeProperty);
    public static void SetMode(DependencyObject d, string value) => d.SetValue(ModeProperty, value);

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box) return;
        box.PreviewTextInput -= OnPreviewTextInput;
        box.PreviewKeyDown -= OnPreviewKeyDown;
        DataObject.RemovePastingHandler(box, OnPaste);
        if (e.NewValue is string mode && mode.Length > 0)
        {
            box.PreviewTextInput += OnPreviewTextInput;
            box.PreviewKeyDown += OnPreviewKeyDown;
            DataObject.AddPastingHandler(box, OnPaste);
            if (mode != "Phone") InputMethod.SetIsInputMethodEnabled(box, false);
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var box = (TextBox)sender;
        var newText = box.Text.Remove(box.SelectionStart, box.SelectionLength).Insert(box.SelectionStart, e.Text);
        e.Handled = !IsValid(GetMode(box), newText);
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // The space key does not raise PreviewTextInput reliably, so block it here
        if (e.Key == Key.Space && GetMode((TextBox)sender) != "Phone") e.Handled = true;
    }

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        var box = (TextBox)sender;
        if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText)) { e.CancelCommand(); return; }
        var pasted = (string)e.DataObject.GetData(DataFormats.UnicodeText) ?? "";
        var newText = box.Text.Remove(box.SelectionStart, box.SelectionLength).Insert(box.SelectionStart, pasted.Trim());
        if (!IsValid(GetMode(box), newText)) e.CancelCommand();
    }

    private static bool IsDigit(char c) => c >= '0' && c <= '9';

    public static bool IsValid(string mode, string text)
    {
        switch (mode)
        {
            case "Integer":
                return text.All(c => IsDigit(c) || c == ',');
            case "Digits":
                return text.All(IsDigit);
            case "Decimal":
                return text.All(c => IsDigit(c) || c == '.') && text.Count(c => c == '.') <= 1;
            case "Phone":
                return text.All(c => IsDigit(c) || c == '+' || c == '-' || c == ' ');
            default:
                return true;
        }
    }
}
