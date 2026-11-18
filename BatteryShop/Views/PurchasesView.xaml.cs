using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using BatteryShop.Data;
using BatteryShop.Helpers;
using BatteryShop.Models;

namespace BatteryShop.Views;

public partial class PurchasesView : UserControl
{
    private readonly ObservableCollection<PurchaseLine> _lines = new();

    public PurchasesView()
    {
        InitializeComponent();
        LinesGrid.ItemsSource = _lines;
        MethodBox.ItemsSource = PaymentMethods.All;
        MethodBox.SelectedIndex = 0;
        DateBox.SelectedDate = DateTime.Today;
        FromDate.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        ToDate.SelectedDate = DateTime.Today;
        Loaded += (_, _) => Init();
    }

    private void Init()
    {
        SupplierBox.ItemsSource = PartyRepo.GetSuppliers();
        LineModel.ItemsSource = CatalogRepo.GetModels();
        LoadHistory();
    }

    private void LineModel_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Suggest the last cost paid for this model
        if (LineModel.SelectedItem is BatteryModel m && string.IsNullOrWhiteSpace(LineCost.Text))
        {
            var last = StockRepo.GetUnits(m.Id, status: null, condition: "New").LastOrDefault();
            if (last != null) LineCost.Text = last.UnitCost.ToString("N0");
        }
    }

    private void AddLine_Click(object sender, RoutedEventArgs e)
    {
        UI.Run(() =>
        {
            if (LineModel.SelectedItem is not BatteryModel m)
                throw new AppException("Select a battery model from the list. New models are added under 'Battery Models'.");
            var line = new PurchaseLine
            {
                Model = m, Quantity = UI.Positive(LineQty.Text, "Quantity"), UnitCost = UI.Positive(LineCost.Text, "Cost each"),
                Serials = LineSerials.Text.Trim()
            };
            if (line.SerialList().Count > line.Quantity)
                throw new AppException($"{line.SerialList().Count} serial numbers entered but quantity is {line.Quantity}.");
            _lines.Add(line);
            LineModel.SelectedItem = null;
            LineModel.Text = "";
            LineQty.Text = "1";
            LineCost.Text = LineSerials.Text = "";
            LineModel.Focus();
            UpdateTotals();
        });
    }

    private void RemoveLine_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is PurchaseLine l) { _lines.Remove(l); UpdateTotals(); }
    }

    private void Totals_Changed(object sender, TextChangedEventArgs e) => UpdateTotals();

    private void UpdateTotals()
    {
        if (!IsInitialized || SubTotalText == null || NetText == null || DiscountBox == null) return;
        var sub = _lines.Sum(l => l.LineTotal);
        SubTotalText.Text = sub.ToString("N0");
        NetText.Text = (sub - UI.MoneyOrZero(DiscountBox.Text)).ToString("N0");
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_lines.Count > 0 && !UI.Confirm("Clear this purchase?")) return;
        ClearForm();
    }

    private void ClearForm()
    {
        _lines.Clear();
        BillNoBox.Text = NotesBox.Text = "";
        DiscountBox.Text = PaidBox.Text = "0";
        DateBox.SelectedDate = DateTime.Today;
        UpdateTotals();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        UI.Run(() =>
        {
            if (SupplierBox.SelectedItem is not Supplier s) throw new AppException("Please select a supplier. Add suppliers under 'Suppliers'.");
            var date = DateBox.SelectedDate ?? DateTime.Today;
            if (date > DateTime.Today) throw new AppException("Purchase date cannot be in the future.");
            var no = PurchaseRepo.SavePurchase(s.Id, string.IsNullOrWhiteSpace(BillNoBox.Text) ? null : BillNoBox.Text.Trim(), date,
                _lines.ToList(), UI.NonNegative(DiscountBox.Text, "Discount"), UI.NonNegative(PaidBox.Text, "Paid now"),
                MethodBox.SelectedItem as string, string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text.Trim());
            UI.Info($"Purchase {no} saved. {_lines.Sum(l => l.Quantity)} battery(ies) added to stock.");
            ClearForm();
            LoadHistory();
        });
    }

    // ---------------------------------------------------------------- History
    private void LoadHistory()
    {
        var list = PurchaseRepo.GetPurchases(FromDate.SelectedDate ?? DateTime.Today, ToDate.SelectedDate ?? DateTime.Today);
        foreach (var p in list) p.PurchaseDate = UI.FormatDateTime(p.PurchaseDate);
        HistoryGrid.ItemsSource = list;
        HistoryTotal.Text = $"{list.Count(p => p.Status == "Active")} purchase(s)   |   Total: {UI.Rs(list.Where(p => p.Status == "Active").Sum(p => p.NetTotal))}";
    }

    private void ShowHistory_Click(object sender, RoutedEventArgs e) => LoadHistory();

    private void Void_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not PurchaseRow p) { UI.Info("Select a purchase first."); return; }
        if (p.Status != "Active") { UI.Info("This purchase is already cancelled."); return; }
        var reason = UI.Prompt("Cancel purchase", $"Cancel purchase {p.PurchaseNo}? Its batteries will be removed from stock.\n\nReason:");
        if (reason == null) return;
        UI.Run(() => { PurchaseRepo.VoidPurchase(p.Id, reason); LoadHistory(); });
    }
}
