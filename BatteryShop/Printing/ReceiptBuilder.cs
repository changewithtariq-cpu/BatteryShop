using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BatteryShop.Data;
using BatteryShop.Helpers;
using BatteryShop.Models;

namespace BatteryShop.Printing;

/// <summary>
/// Builds 80mm receipts as WPF visuals. Height grows automatically with the content.
/// Each Build... method returns a function so a fresh copy can be made for preview and for printing.
/// </summary>
public static class ReceiptBuilder
{
    /// <summary>Printable width of 80mm paper is about 72mm = 272 px at 96 dpi.</summary>
    public const double PaperWidth = 272;

    private const string FontName = "Segoe UI";
    private static readonly Brush Ink = Brushes.Black;

    // ================================================================ Sale receipt
    public static Func<FrameworkElement> BuildSaleReceipt(SaleReceipt r) => () =>
    {
        var shop = CatalogRepo.GetSettings();
        var s = r.Sale;
        var p = NewPage();
        Header(p, shop);

        var title = s.Status == "Voided" ? "*** CANCELLED ***" : r.PrintCount > 0 ? "SALE RECEIPT (DUPLICATE)" : "SALE RECEIPT";
        Text(p, title, 13, FontWeights.Bold, TextAlignment.Center, new Thickness(0, 2, 0, 4));
        Row(p, "Bill No:", s.InvoiceNo, bold: true);
        Row(p, "Date:", UI.FormatDateTime(s.SaleDate));
        Row(p, "Customer:", s.CustomerName);
        if (!string.IsNullOrWhiteSpace(s.Phone)) Row(p, "Phone:", s.Phone);
        Line(p);

        foreach (var i in r.Items)
        {
            Text(p, i.Description, 12.5, FontWeights.SemiBold);
            var sub = string.IsNullOrEmpty(i.SerialNo) ? "" : "S/N: " + i.SerialNo;
            if (i.Condition == "SecondHand") sub += (sub.Length > 0 ? "   " : "") + "(Used)";
            if (sub.Length > 0) Text(p, sub, 11);
            Row(p, "Price", i.UnitPrice.ToString("N0"));
            if (i.Discount > 0) Row(p, "Discount", "-" + i.Discount.ToString("N0"));
            if (i.WarrantyMonths > 0)
                Text(p, $"Warranty: {i.WarrantyMonths} months (till {UI.FormatDate(i.WarrantyExpiry)})", 11, FontWeights.SemiBold);
            else
                Text(p, "Warranty: none", 11);
            Spacer(p, 4);
        }
        Line(p);

        Row(p, "Sub Total", s.SubTotal.ToString("N0"));
        if (s.Discount > 0) Row(p, "Discount", "-" + s.Discount.ToString("N0"));
        foreach (var e in r.Exchanges)
        {
            var label = "Old battery" + (e.WeightKg.HasValue ? $" ({e.WeightKg:0.#} kg)" : "");
            Row(p, label, "-" + e.ExchangeValue.ToString("N0"));
        }
        Line(p);
        Row(p, "NET TOTAL", "Rs. " + s.NetTotal.ToString("N0"), bold: true, size: 15);
        Row(p, "Paid" + (r.Payments.Count > 0 ? $" ({r.Payments[0].Method})" : ""), s.PaidAmount.ToString("N0"));
        Row(p, "Remaining", "Rs. " + s.RemainingAmount.ToString("N0"), bold: true, size: 13.5);

        if (r.Customer != null && !r.Customer.IsWalkIn && r.CustomerTotalBalance != s.RemainingAmount)
            Row(p, "Total account balance", "Rs. " + r.CustomerTotalBalance.ToString("N0"), bold: true);

        Footer(p, shop.ReceiptFooter);
        return p;
    };

    // ================================================================ Payment receipt
    public static Func<FrameworkElement> BuildPaymentReceipt(PaymentRow pay, Customer c, long previousBalance, long newBalance) => () =>
    {
        var shop = CatalogRepo.GetSettings();
        var p = NewPage();
        Header(p, shop);
        Text(p, "PAYMENT RECEIPT", 13, FontWeights.Bold, TextAlignment.Center, new Thickness(0, 2, 0, 4));
        Row(p, "Receipt No:", pay.ReceiptNo, bold: true);
        Row(p, "Date:", UI.FormatDateTime(pay.PaymentDate));
        Row(p, "Customer:", c.Name);
        if (!string.IsNullOrWhiteSpace(c.Phone)) Row(p, "Phone:", c.Phone);
        Line(p);
        Row(p, "Previous balance", previousBalance.ToString("N0"));
        Row(p, $"Received ({pay.Method})", "Rs. " + pay.Amount.ToString("N0"), bold: true, size: 14);
        Line(p);
        Row(p, "Balance now", "Rs. " + newBalance.ToString("N0"), bold: true, size: 14);
        if (!string.IsNullOrWhiteSpace(pay.Notes)) Text(p, "Note: " + pay.Notes, 11);
        Footer(p, null);
        return p;
    };

    // ================================================================ Statement
    public static Func<FrameworkElement> BuildStatement(string party, string subtitle, List<LedgerRow> rows, string closingLabel) => () =>
    {
        var shop = CatalogRepo.GetSettings();
        var p = NewPage();
        Header(p, shop);
        Text(p, "ACCOUNT STATEMENT", 13, FontWeights.Bold, TextAlignment.Center, new Thickness(0, 2, 0, 2));
        Text(p, party, 12.5, FontWeights.SemiBold, TextAlignment.Center);
        Text(p, subtitle, 10.5, FontWeights.Normal, TextAlignment.Center);
        Line(p);
        foreach (var r in rows)
        {
            var amount = r.Debit > 0 ? "+" + r.Debit.ToString("N0") : r.Credit > 0 ? "-" + r.Credit.ToString("N0") : "";
            Row(p, $"{(string.IsNullOrEmpty(r.TxnDate) ? "" : r.TxnDate.Split(' ')[0] + "  ")}{r.TxnType}", amount, size: 11);
            Row(p, "   " + (r.RefNo ?? ""), "Bal " + r.Balance.ToString("N0"), size: 10.5);
        }
        Line(p);
        Row(p, closingLabel, "Rs. " + (rows.LastOrDefault()?.Balance ?? 0).ToString("N0"), bold: true, size: 14);
        Footer(p, null);
        return p;
    };

    // ================================================================ Test page
    public static Func<FrameworkElement> BuildTestPage() => () =>
    {
        var shop = CatalogRepo.GetSettings();
        var p = NewPage();
        Header(p, shop);
        Text(p, "PRINTER TEST", 13, FontWeights.Bold, TextAlignment.Center);
        Row(p, "Left text", "Right text");
        Row(p, "NET TOTAL", "Rs. 18,500", bold: true, size: 15);
        Text(p, "If this text is not cut off on the left or right, the printer is set up correctly.", 11);
        Footer(p, shop.ReceiptFooter);
        return p;
    };

    // ================================================================ Building blocks
    private static StackPanel NewPage() => new()
    {
        Width = PaperWidth, Background = Brushes.White, Margin = new Thickness(0)
    };

    private static void Header(StackPanel p, ShopSettings shop)
    {
        Text(p, shop.ShopName ?? "", 17, FontWeights.Bold, TextAlignment.Center);
        if (!string.IsNullOrWhiteSpace(shop.Address)) Text(p, shop.Address, 10.5, FontWeights.Normal, TextAlignment.Center);
        if (!string.IsNullOrWhiteSpace(shop.Phone)) Text(p, "Ph: " + shop.Phone, 11.5, FontWeights.SemiBold, TextAlignment.Center);
        Line(p);
    }

    private static void Footer(StackPanel p, string footer)
    {
        Line(p);
        if (!string.IsNullOrWhiteSpace(footer)) Text(p, footer, 10.5, FontWeights.Normal, TextAlignment.Center);
        Text(p, "Thank you for your business!", 11.5, FontWeights.SemiBold, TextAlignment.Center, new Thickness(0, 4, 0, 0));
        Text(p, "Printed " + DateTime.Now.ToString("dd-MMM-yyyy hh:mm tt"), 9, FontWeights.Normal, TextAlignment.Center);
        Spacer(p, 18);   // room before the cutter
    }

    private static void Text(StackPanel p, string text, double size = 12, FontWeight? weight = null,
                             TextAlignment align = TextAlignment.Left, Thickness? margin = null)
    {
        p.Children.Add(new TextBlock
        {
            Text = text, FontSize = size, FontWeight = weight ?? FontWeights.Normal, TextAlignment = align,
            TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily(FontName), Foreground = Ink,
            Margin = margin ?? new Thickness(0, 1, 0, 1)
        });
    }

    private static void Row(StackPanel p, string left, string right, bool bold = false, double size = 12)
    {
        var g = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var weight = bold ? FontWeights.Bold : FontWeights.Normal;
        var l = new TextBlock { Text = left, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap,
                                FontFamily = new FontFamily(FontName), Foreground = Ink };
        var r = new TextBlock { Text = right, FontSize = size, FontWeight = weight, TextAlignment = TextAlignment.Right,
                                FontFamily = new FontFamily(FontName), Foreground = Ink, Margin = new Thickness(6, 0, 0, 0) };
        Grid.SetColumn(r, 1);
        g.Children.Add(l);
        g.Children.Add(r);
        p.Children.Add(g);
    }

    private static void Line(StackPanel p) => p.Children.Add(new Border
    {
        BorderBrush = Ink, BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 4, 0, 4), Height = 1
    });

    private static void Spacer(StackPanel p, double h) => p.Children.Add(new Border { Height = h });
}
