using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BatteryShop.Data;
using BatteryShop.Helpers;
using BatteryShop.Models;
using BatteryShop.Printing;

namespace BatteryShop.Views;

public partial class NewSaleView : UserControl
{
    private readonly ObservableCollection<SaleLine> _lines = new();
    private readonly ObservableCollection<ExchangeLine> _exchanges = new();
    private Customer _walkIn;
    private List<BatteryUnit> _stock = new();   // all batteries in stock, loaded once per bill
    private bool _paidFollowsTotal = true;   // "Paid now" follows the bill total until the user types a different amount
    private bool _settingPaid;

    public NewSaleView()
    {
        InitializeComponent();
        LinesGrid.ItemsSource = _lines;
        ExchangeGrid.ItemsSource = _exchanges;
        MethodBox.ItemsSource = PaymentMethods.All;
        MethodBox.SelectedIndex = 0;
        PreviewKeyDown += OnKey;
        Loaded += (_, _) => { if (CustomerBox.ItemsSource == null) Init(); };
    }

    private void Init()
    {
        _walkIn = PartyRepo.GetWalkInCustomer();
        LoadCustomers(_walkIn?.Id);
        LoadUnits();
        UpdateTotals();
        SearchBox.Focus();
    }

    private void LoadCustomers(long? selectId)
    {
        var list = PartyRepo.GetCustomers();
        CustomerBox.ItemsSource = list;
        CustomerBox.SelectedItem = list.FirstOrDefault(c => c.Id == selectId);
    }

    private void LoadUnits()
    {
        _stock = StockRepo.GetUnits(status: "InStock");
        SearchBox.Text = "";
        ApplySearch();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F9) { SaveSale(print: true); e.Handled = true; }
    }

    // ================================================================ Customer
    private Customer SelectedCustomer => CustomerBox.SelectedItem as Customer;

    private void CustomerBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var c = SelectedCustomer;
        if (c == null) { CustomerBalanceText.Text = ""; return; }
        if (c.IsWalkIn)
        {
            CustomerBalanceText.Text = "Cash sale";
            CustomerBalanceText.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
        }
        else
        {
            CustomerBalanceText.Text = "Old balance: " + UI.Rs(c.Balance);
            CustomerBalanceText.Foreground = (System.Windows.Media.Brush)FindResource(c.Balance > 0 ? "DangerBrush" : "SuccessBrush");
        }
        // Named customers often pay partly; walk-in must pay in full
        _paidFollowsTotal = true;
        UpdateTotals();
    }

    private void WalkIn_Click(object sender, RoutedEventArgs e)
        => CustomerBox.SelectedItem = ((IEnumerable<Customer>)CustomerBox.ItemsSource).FirstOrDefault(c => c.IsWalkIn);

    private void NewCustomer_Click(object sender, RoutedEventArgs e)
    {
        var name = UI.Prompt("New customer", "Customer name:");
        if (string.IsNullOrWhiteSpace(name)) return;
        var phone = UI.Prompt("New customer", $"Phone number for {name} (optional):");
        if (phone == null) return;
        UI.Run(() =>
        {
            var id = PartyRepo.SaveCustomer(new Customer { Name = name, Phone = phone });
            LoadCustomers(id);
        });
    }

    // ================================================================ Battery search

    /// <summary>Text that a battery can be found by: brand, model, Ah, serial, condition, note.</summary>
    private static string SearchText(BatteryUnit u) =>
        $"{u.ModelDisplay} {u.SerialNo} {u.Condition} {(u.Condition == "SecondHand" ? "used old" : "new")} {u.ConditionNote}".ToLowerInvariant();

    /// <summary>Every word typed must appear somewhere (e.g. "ags 60" finds AGS batteries with 60 in the name or Ah).</summary>
    private void ApplySearch()
    {
        if (ResultsGrid == null) return;
        var words = SearchBox.Text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var onBill = _lines.Select(l => l.Unit.Id).ToHashSet();
        var results = _stock.Where(u => !onBill.Contains(u.Id))
                            .Where(u => { var t = SearchText(u); return words.All(w => t.Contains(w)); })
                            .ToList();
        ResultsGrid.ItemsSource = results;
        if (results.Count > 0) ResultsGrid.SelectedIndex = 0;
        ResultCountText.Text = $"{results.Count} in stock";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplySearch();

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var results = ResultsGrid.ItemsSource as List<BatteryUnit> ?? new();
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var text = SearchBox.Text.Trim();
            if (text.Length == 0) return;
            // A scanned / typed serial number adds that battery straight away
            var exact = results.FirstOrDefault(u => string.Equals(u.SerialNo, text, StringComparison.OrdinalIgnoreCase));
            if (exact != null) { AddAndReset(exact); return; }
            if (results.Count == 1) { AddAndReset(results[0]); return; }
            if (results.Count == 0) { UI.Info($"No battery in stock matches '{text}'."); SearchBox.SelectAll(); return; }
            FocusResults();   // several matches: let the user choose
        }
        else if (e.Key == Key.Down && results.Count > 0)
        {
            e.Handled = true;
            FocusResults();
        }
    }

    private void FocusResults()
    {
        if (ResultsGrid.Items.Count == 0) return;
        if (ResultsGrid.SelectedIndex < 0) ResultsGrid.SelectedIndex = 0;
        ResultsGrid.UpdateLayout();
        ResultsGrid.ScrollIntoView(ResultsGrid.SelectedItem);
        if (ResultsGrid.ItemContainerGenerator.ContainerFromIndex(ResultsGrid.SelectedIndex) is DataGridRow row)
            row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        else
            ResultsGrid.Focus();
    }

    private void ResultsGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ResultsGrid.SelectedItem is BatteryUnit u) { e.Handled = true; AddAndReset(u); }
        else if (e.Key == Key.Up && ResultsGrid.SelectedIndex == 0) { e.Handled = true; SearchBox.Focus(); }
        else if (e.Key == Key.Escape) { e.Handled = true; SearchBox.Focus(); }
    }

    private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsGrid.SelectedItem is BatteryUnit u) AddAndReset(u);
    }

    private void AddAndReset(BatteryUnit unit)
    {
        if (!AddUnit(unit)) return;
        SearchBox.Text = "";   // also refreshes the list (added battery disappears)
        SearchBox.Focus();
    }

    private bool AddUnit(BatteryUnit unit)
    {
        if (_lines.Any(l => l.Unit.Id == unit.Id)) { UI.Info("This battery is already on the bill."); return false; }
        var line = new SaleLine
        {
            Unit = unit,
            UnitPrice = unit.SuggestedPrice,
            WarrantyMonths = unit.Condition == "New" ? unit.WarrantyMonths : 0
        };
        line.PropertyChanged += (_, _) => UpdateTotals();
        _lines.Add(line);
        UpdateTotals();
        return true;
    }

    private void RemoveLine_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SaleLine l) { _lines.Remove(l); UpdateTotals(); ApplySearch(); }
    }

    private void LinesGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        => Dispatcher.BeginInvoke(new Action(UpdateTotals), DispatcherPriority.Background);

    // ================================================================ Exchange
    private void AddExchange_Click(object sender, RoutedEventArgs e)
    {
        UI.Run(() =>
        {
            var value = UI.Positive(ExValue.Text, "Old battery value");
            var weight = UI.Decimal(ExWeight.Text, "Weight");
            if (weight.HasValue && (weight <= 0 || weight > 200)) throw new AppException("Weight must be between 0 and 200 kg (or leave it empty).");
            _exchanges.Add(new ExchangeLine
            {
                Description = string.IsNullOrWhiteSpace(ExDesc.Text) ? "Old battery" : ExDesc.Text.Trim(),
                WeightKg = weight,
                ExchangeValue = value
            });
            ExDesc.Text = "Old battery";
            ExWeight.Text = ExValue.Text = "";
            UpdateTotals();
        });
    }

    private void RemoveExchange_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ExchangeLine x) { _exchanges.Remove(x); UpdateTotals(); }
    }

    // ================================================================ Totals
    private long Net => _lines.Sum(l => l.LineTotal) - UI.MoneyOrZero(DiscountBox.Text) - _exchanges.Sum(x => x.ExchangeValue);

    private void Amount_Changed(object sender, TextChangedEventArgs e)
    {
        if (IsInitialized && sender == PaidBox && !_settingPaid) _paidFollowsTotal = false;
        UpdateTotals();
    }

    private void FullPaid_Click(object sender, RoutedEventArgs e)
    {
        _paidFollowsTotal = true;
        UpdateTotals();
    }

    private void UpdateTotals()
    {
        if (!IsInitialized || NetText == null || PaidBox == null) return;
        var sub = _lines.Sum(l => l.LineTotal);
        var exchange = _exchanges.Sum(x => x.ExchangeValue);
        var net = Net;

        SubTotalText.Text = sub.ToString("N0");
        ExchangeText.Text = exchange > 0 ? "-" + exchange.ToString("N0") : "0";
        NetText.Text = UI.Rs(net);

        if (_paidFollowsTotal)
        {
            _settingPaid = true;
            PaidBox.Text = Math.Max(net, 0).ToString("N0");
            _settingPaid = false;
        }

        var paid = UI.MoneyOrZero(PaidBox.Text);
        var remaining = net - paid;
        RemainingText.Text = UI.Rs(remaining);
        PaymentTypeText.Text = net <= 0 ? "" : paid >= net ? "Full cash payment" : paid == 0 ? "Full credit (udhaar)" : "Partial payment";
    }

    // ================================================================ Save
    private void SavePrint_Click(object sender, RoutedEventArgs e) => SaveSale(print: true);
    private void Save_Click(object sender, RoutedEventArgs e) => SaveSale(print: false);

    private void SaveSale(bool print)
    {
        // Commit any cell the user is still typing in
        LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);

        long saleId = 0;
        var ok = UI.Run(() =>
        {
            if (SelectedCustomer == null)
                throw new AppException("Please select a customer from the list (or click Walk-in / + New customer).");
            saleId = SaleRepo.SaveSale(SelectedCustomer.Id, _lines.ToList(), _exchanges.ToList(),
                UI.NonNegative(DiscountBox.Text, "Bill discount"), UI.NonNegative(PaidBox.Text, "Paid now"),
                MethodBox.SelectedItem as string, string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text.Trim());
        });
        if (!ok) return;

        var customerId = SelectedCustomer.Id;
        if (print)
        {
            var receipt = SaleRepo.GetReceipt(saleId);
            ReceiptPrinter.PrintOrPreview(ReceiptBuilder.BuildSaleReceipt(receipt), () => SaleRepo.MarkPrinted(saleId));
        }
        else
        {
            var s = SaleRepo.GetReceipt(saleId).Sale;
            UI.Info($"Sale {s.InvoiceNo} saved.\nTotal {UI.Rs(s.NetTotal)}, paid {UI.Rs(s.PaidAmount)}, remaining {UI.Rs(s.RemainingAmount)}.");
        }

        ClearBill();
        LoadCustomers(_walkIn?.Id);
        LoadUnits();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if ((_lines.Count > 0 || _exchanges.Count > 0) && !UI.Confirm("Clear this bill?")) return;
        ClearBill();
        CustomerBox.SelectedItem = ((IEnumerable<Customer>)CustomerBox.ItemsSource)?.FirstOrDefault(c => c.IsWalkIn);
    }

    private void ClearBill()
    {
        _lines.Clear();
        _exchanges.Clear();
        DiscountBox.Text = "0";
        NotesBox.Text = "";
        MethodBox.SelectedIndex = 0;
        _paidFollowsTotal = true;
        UpdateTotals();
        ApplySearch();
        SearchBox.Focus();
    }
}
