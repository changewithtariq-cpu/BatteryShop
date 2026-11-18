using System.Windows;
using System.Windows.Controls;
using BatteryShop.Data;
using BatteryShop.Helpers;
using BatteryShop.Models;

namespace BatteryShop.Views;

public partial class AddStockDialog : Window
{
    private static readonly string[] Sources = { "Opening stock", "Bought / other" };

    public AddStockDialog()
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
        ModelBox.ItemsSource = CatalogRepo.GetModels();
        ConditionBox.ItemsSource = new[] { "New", "SecondHand" };
        SourceBox.ItemsSource = Sources;
        SourceBox.SelectedIndex = 0;
        ConditionBox.SelectedIndex = 0;
    }

    private void Condition_Changed(object sender, SelectionChangedEventArgs e)
    {
        var secondHand = (string)ConditionBox.SelectedItem == "SecondHand";
        PriceLabel.Text = secondHand ? "Sale price *" : "Sale price (optional)";
        if (secondHand) SourceBox.SelectedIndex = 1;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        UI.Run(() =>
        {
            if (ModelBox.SelectedItem is not BatteryModel model)
                throw new AppException("Please select a battery model from the list. Add new models under 'Battery Models'.");
            var condition = (string)ConditionBox.SelectedItem;
            var qty = UI.Positive(QtyBox.Text, "Quantity");
            var cost = UI.Positive(CostBox.Text, "Cost per battery");
            long? price = string.IsNullOrWhiteSpace(PriceBox.Text) ? null : UI.Positive(PriceBox.Text, "Sale price");
            if (condition == "SecondHand" && price == null) throw new AppException("Please enter a sale price for the second-hand battery.");
            var serials = new PurchaseLine { Serials = SerialsBox.Text }.SerialList();
            var source = SourceBox.SelectedIndex == 0 ? "Opening" : "Manual";

            StockRepo.AddManualUnits(model.Id, condition, source, qty, serials, cost, price,
                string.IsNullOrWhiteSpace(NoteBox.Text) ? null : NoteBox.Text.Trim());
            DialogResult = true;
        });
    }
}
