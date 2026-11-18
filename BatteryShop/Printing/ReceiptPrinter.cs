using System.Printing;
using System.Windows;
using System.Windows.Controls;
using BatteryShop.Data;
using BatteryShop.Helpers;

namespace BatteryShop.Printing;

/// <summary>
/// Sends receipts to the printer chosen in Settings. Uses normal Windows printing,
/// so it works with any 80mm thermal printer that has a Windows driver.
/// </summary>
public static class ReceiptPrinter
{
    public static List<string> InstalledPrinters()
    {
        try
        {
            using var server = new LocalPrintServer();
            return server.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections })
                         .Select(q => q.FullName).OrderBy(n => n).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Prints straight to the receipt printer. If no printer is set in Settings,
    /// the Windows print dialog is shown. Returns true if it was sent to a printer.
    /// </summary>
    public static bool Print(Func<FrameworkElement> build, string jobName = "Receipt")
    {
        var pd = new PrintDialog();
        var printerName = CatalogRepo.GetSettings().ReceiptPrinterName;
        var queue = FindQueue(printerName);
        if (queue != null)
            pd.PrintQueue = queue;
        else if (pd.ShowDialog() != true)
            return false;

        var visual = build();
        // Fit inside the printer's printable width (some drivers report a little less than 72mm)
        var width = Math.Min(ReceiptBuilder.PaperWidth, pd.PrintableAreaWidth > 50 ? pd.PrintableAreaWidth - 6 : ReceiptBuilder.PaperWidth);
        visual.Width = width;
        visual.Measure(new Size(width, double.PositiveInfinity));
        visual.Arrange(new Rect(new Point(0, 0), visual.DesiredSize));
        visual.UpdateLayout();
        pd.PrintVisual(visual, jobName);
        return true;
    }

    private static PrintQueue FindQueue(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        try
        {
            var server = new LocalPrintServer();   // kept alive: the queue uses it
            return server.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections })
                         .FirstOrDefault(q => q.FullName == name);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Shows the receipt on screen with a Print button.</summary>
    public static void PreviewAndPrint(Func<FrameworkElement> build, Action onPrinted)
    {
        new ReceiptPreviewWindow(build, onPrinted).ShowDialog();
    }

    /// <summary>Prints directly; if that fails (printer off / not set), shows the preview instead.</summary>
    public static void PrintOrPreview(Func<FrameworkElement> build, Action onPrinted)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CatalogRepo.GetSettings().ReceiptPrinterName))
            {
                PreviewAndPrint(build, onPrinted);
                return;
            }
            if (Print(build)) onPrinted?.Invoke();
        }
        catch (Exception ex)
        {
            UI.Info("Could not print the receipt: " + ex.Message + "\n\nThe receipt will be shown on screen so you can try again.");
            PreviewAndPrint(build, onPrinted);
        }
    }
}
