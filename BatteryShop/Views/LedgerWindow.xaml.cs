using System.Windows;
using BatteryShop.Helpers;
using BatteryShop.Models;
using BatteryShop.Printing;

namespace BatteryShop.Views;

/// <summary>Account statement for a customer or supplier.</summary>
public partial class LedgerWindow : Window
{
    private readonly Func<DateTime?, DateTime?, List<LedgerRow>> _loader;
    private readonly string _party;
    private readonly string _closingLabel;
    private List<LedgerRow> _rows = new();

    public LedgerWindow(string party, string subtitle, string debitHeader, string creditHeader, string closingLabel,
                        Func<DateTime?, DateTime?, List<LedgerRow>> loader)
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
        _loader = loader;
        _party = party;
        _closingLabel = closingLabel;
        TitleText.Text = party;
        SubText.Text = subtitle;
        DebitCol.Header = debitHeader;
        CreditCol.Header = creditHeader;
        Loaded += (_, _) => Load(null, null);
    }

    private void Load(DateTime? from, DateTime? to)
    {
        _rows = _loader(from, to);
        foreach (var r in _rows) r.TxnDate = string.IsNullOrEmpty(r.TxnDate) ? "" : UI.FormatDateTime(r.TxnDate);
        LedgerGrid.ItemsSource = _rows;
        var closing = _rows.LastOrDefault()?.Balance ?? 0;
        ClosingText.Text = $"{_closingLabel}: {UI.Rs(closing)}";
    }

    private void Show_Click(object sender, RoutedEventArgs e)
    {
        if (FromDate.SelectedDate == null || ToDate.SelectedDate == null) { UI.Info("Please select both dates."); return; }
        Load(FromDate.SelectedDate, ToDate.SelectedDate);
    }

    private void All_Click(object sender, RoutedEventArgs e)
    {
        FromDate.SelectedDate = ToDate.SelectedDate = null;
        Load(null, null);
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        UI.Run(() => ReceiptPrinter.PreviewAndPrint(ReceiptBuilder.BuildStatement(_party, SubText.Text, _rows, _closingLabel), null));
    }
}
