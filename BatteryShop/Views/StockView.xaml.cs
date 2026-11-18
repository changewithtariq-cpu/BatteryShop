using System.Windows;
using System.Windows.Controls;
using BatteryShop.Data;
using BatteryShop.Helpers;
using BatteryShop.Models;

namespace BatteryShop.Views;

public partial class StockView : UserControl
{
    private bool _ready;

    public StockView()
    {
        InitializeComponent();
        Loaded += (_, _) => Init();
    }

    private void Init()
    {
        _ready = false;
        var models = new List<object> { "All models" };
        models.AddRange(CatalogRepo.GetModels(activeOnly: false));
        FilterModel.ItemsSource = models;
        FilterModel.SelectedIndex = 0;
        FilterCondition.ItemsSource = new[] { "All conditions", "New", "SecondHand" };
        FilterCondition.SelectedIndex = 0;
        FilterStatus.ItemsSource = new[] { "InStock", "Sold", "Removed", "All statuses" };
        FilterStatus.SelectedIndex = 0;
        _ready = true;
        LoadModelStock();
        LoadUnits();
    }

    private void LoadModelStock()
    {
        var list = StockRepo.GetModelStock();
        ModelStockGrid.ItemsSource = list;
        SummaryText.Text = $"New: {list.Sum(s => s.NewInStock):N0}   |   Second-hand: {list.Sum(s => s.SecondHandInStock):N0}   |   " +
                           $"Value at cost: {UI.Rs(list.Sum(s => s.StockValueAtCost))}";
    }

    private void LoadUnits()
    {
        if (!_ready) return;
        long? modelId = (FilterModel.SelectedItem as BatteryModel)?.Id;
        var cond = FilterCondition.SelectedIndex > 0 ? (string)FilterCondition.SelectedItem : null;
        var status = (string)FilterStatus.SelectedItem == "All statuses" ? null : (string)FilterStatus.SelectedItem;
        var list = StockRepo.GetUnits(modelId, status, cond, SearchBox.Text);
        foreach (var u in list) u.ReceivedDate = UI.FormatDate(u.ReceivedDate);
        UnitGrid.ItemsSource = list;
        UnitCountText.Text = $"{list.Count} battery(ies)";
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => LoadUnits();
    private void Search_TextChanged(object sender, TextChangedEventArgs e) => LoadUnits();

    private void ModelStockGrid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ModelStockGrid.SelectedItem is not ModelStock s) return;
        FilterModel.SelectedItem = ((IEnumerable<object>)FilterModel.ItemsSource).OfType<BatteryModel>().FirstOrDefault(m => m.Id == s.ModelId);
        Tabs.SelectedIndex = 1;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (new AddStockDialog().ShowDialog() == true) { LoadModelStock(); LoadUnits(); }
    }

    private void Purchase_Click(object sender, RoutedEventArgs e) => MainWindow.Instance.Navigate("Purchases");

    private BatteryUnit SelectedUnit()
    {
        if (UnitGrid.SelectedItem is BatteryUnit u) return u;
        UI.Info("Select a battery in the 'Individual batteries' list first.");
        return null;
    }

    private void EditSerial_Click(object sender, RoutedEventArgs e)
    {
        var u = SelectedUnit();
        if (u == null) return;
        var serial = UI.Prompt("Edit serial number", $"Serial number for {u.ModelDisplay}:", u.SerialNo ?? "");
        if (serial == null) return;
        UI.Run(() => { u.SerialNo = serial; StockRepo.UpdateUnit(u); LoadUnits(); });
    }

    private void SetPrice_Click(object sender, RoutedEventArgs e)
    {
        var u = SelectedUnit();
        if (u == null) return;
        var text = UI.Prompt("Sale price", $"Sale price for this battery (leave empty to use the model price Rs. {u.DefaultSalePrice:N0}):",
            u.SalePrice?.ToString() ?? "");
        if (text == null) return;
        UI.Run(() =>
        {
            u.SalePrice = string.IsNullOrWhiteSpace(text) ? null : UI.Positive(text, "Sale price");
            StockRepo.UpdateUnit(u);
            LoadUnits();
        });
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        var u = SelectedUnit();
        if (u == null) return;
        var reason = UI.Prompt("Remove from stock", $"Why is {u.ModelDisplay} ({u.SerialNo ?? "no serial"}) being removed?\n(damaged, lost, entered by mistake...)");
        if (reason == null) return;
        UI.Run(() => { StockRepo.RemoveUnit(u.Id, reason); LoadModelStock(); LoadUnits(); });
    }
}
