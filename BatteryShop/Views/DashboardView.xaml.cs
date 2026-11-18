using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BatteryShop.Data;
using BatteryShop.Helpers;

namespace BatteryShop.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
    }

    private void Load()
    {
        DateText.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy");
        var d = ReportRepo.GetDashboard();

        SalesCards.Children.Clear();
        SalesCards.Children.Add(Card("Today's sales", UI.Rs(d.TodaySales), $"{d.TodayBills} bill(s)"));
        SalesCards.Children.Add(Card("Cash received today", UI.Rs(d.TodayCollection), "sales + old balances"));
        SalesCards.Children.Add(Card("Today's gross profit", UI.Rs(d.TodayProfit), null, d.TodayProfit >= 0 ? "SuccessBrush" : "DangerBrush"));
        SalesCards.Children.Add(Card("This month's sales", UI.Rs(d.MonthSales), DateTime.Today.ToString("MMMM yyyy")));
        SalesCards.Children.Add(Card("This month's gross profit", UI.Rs(d.MonthProfit), null, d.MonthProfit >= 0 ? "SuccessBrush" : "DangerBrush"));

        StockCards.Children.Clear();
        StockCards.Children.Add(Card("Customers owe you", UI.Rs(d.CustomerReceivable), "outstanding balance", "DangerBrush"));
        StockCards.Children.Add(Card("You owe suppliers", UI.Rs(d.SupplierPayable), "payable balance", "WarningBrush"));
        StockCards.Children.Add(Card("New batteries", d.NewStockQty.ToString("N0"), "in stock"));
        StockCards.Children.Add(Card("Second-hand batteries", d.SecondHandStockQty.ToString("N0"), "in stock"));
        StockCards.Children.Add(Card("Stock value", UI.Rs(d.StockValue), "at cost"));

        ExchangeNote.Visibility = d.PendingExchanges > 0 ? Visibility.Visible : Visibility.Collapsed;
        ExchangeText.Text = $"{d.PendingExchanges} old battery(ies) received in exchange are waiting to be sorted into scrap / second-hand stock (Phase 2).";

        LowStockGrid.ItemsSource = d.LowStock;
    }

    private static Border Card(string title, string value, string sub, string valueBrush = "TextBrush")
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = title, Foreground = (Brush)Application.Current.FindResource("MutedBrush") });
        panel.Children.Add(new TextBlock
        {
            Text = value, FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 2),
            Foreground = (Brush)Application.Current.FindResource(valueBrush)
        });
        if (!string.IsNullOrEmpty(sub))
            panel.Children.Add(new TextBlock { Text = sub, FontSize = 11, Foreground = (Brush)Application.Current.FindResource("MutedBrush") });
        return new Border
        {
            Style = (Style)Application.Current.FindResource("Card"), Width = 215, MinHeight = 92,
            Margin = new Thickness(0, 0, 12, 12), Child = panel
        };
    }

    private void NewSale_Click(object sender, RoutedEventArgs e) => MainWindow.Instance.Navigate("NewSale");
    private void Customers_Click(object sender, RoutedEventArgs e) => MainWindow.Instance.Navigate("Customers");
    private void Purchase_Click(object sender, RoutedEventArgs e) => MainWindow.Instance.Navigate("Purchases");
    private void Refresh_Click(object sender, RoutedEventArgs e) => Load();
}
