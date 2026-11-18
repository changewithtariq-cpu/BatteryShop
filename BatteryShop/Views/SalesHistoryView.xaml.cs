using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BatteryShop.Data;
using BatteryShop.Helpers;
using BatteryShop.Models;
using BatteryShop.Printing;

namespace BatteryShop.Views;

public partial class SalesHistoryView : UserControl
{
    private readonly Customer _customer;   // set when opened from Customers > Bills

    public SalesHistoryView() : this(null) { }

    public SalesHistoryView(Customer customer)
    {
        InitializeComponent();
        _customer = customer;
        if (customer != null)
        {
            TitleText.Text = "Bills of " + customer.Display;
            FromDate.SelectedDate = new DateTime(2000, 1, 1);
        }
        else
        {
            FromDate.SelectedDate = DateTime.Today;
        }
        ToDate.SelectedDate = DateTime.Today;
        Loaded += (_, _) => Load();
    }

    private void Load()
    {
        var list = SaleRepo.GetSales(FromDate.SelectedDate ?? DateTime.Today, ToDate.SelectedDate ?? DateTime.Today,
                                     SearchBox.Text, _customer?.Id);
        foreach (var s in list) s.SaleDate = UI.FormatDateTime(s.SaleDate);
        SalesGrid.ItemsSource = list;
        var active = list.Where(s => s.Status == "Active").ToList();
        TotalsText.Text = $"{active.Count} bill(s)   |   Sales {UI.Rs(active.Sum(s => s.NetTotal))}   |   " +
                          $"Received {UI.Rs(active.Sum(s => s.PaidAmount))}   |   Credit {UI.Rs(active.Sum(s => s.RemainingAmount))}   |   " +
                          $"Profit {UI.Rs(active.Sum(s => s.GrossProfit))}";
    }

    private void Show_Click(object sender, RoutedEventArgs e) => Load();
    private void SearchBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Load(); }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        FromDate.SelectedDate = ToDate.SelectedDate = DateTime.Today;
        Load();
    }

    private void Month_Click(object sender, RoutedEventArgs e)
    {
        FromDate.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        ToDate.SelectedDate = DateTime.Today;
        Load();
    }

    private SaleSummary Selected()
    {
        if (SalesGrid.SelectedItem is SaleSummary s) return s;
        UI.Info("Select a bill first.");
        return null;
    }

    private void Reprint_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected();
        if (s == null) return;
        UI.Run(() =>
        {
            var receipt = SaleRepo.GetReceipt(s.SaleId);
            ReceiptPrinter.PreviewAndPrint(ReceiptBuilder.BuildSaleReceipt(receipt), () => SaleRepo.MarkPrinted(s.SaleId));
        });
    }

    private void Void_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected();
        if (s == null) return;
        if (s.Status != "Active") { UI.Info("This bill is already cancelled."); return; }
        var msg = $"Cancel bill {s.InvoiceNo} ({UI.Rs(s.NetTotal)})?\n\n" +
                  "The batteries will go back into stock" +
                  (s.PaidAmount > 0 ? $" and the {UI.Rs(s.PaidAmount)} paid on this bill will be cancelled (return the cash to the customer)." : ".") +
                  "\n\nReason:";
        var reason = UI.Prompt("Cancel sale", msg);
        if (reason == null) return;
        UI.Run(() => { SaleRepo.VoidSale(s.SaleId, reason); Load(); });
    }
}
