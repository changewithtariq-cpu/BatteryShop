using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BatteryShop.Views;

namespace BatteryShop;

public partial class MainWindow : Window
{
    public static MainWindow Instance { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        Instance = this;
        VersionText.Text = "Version " + (GetType().Assembly.GetName().Version?.ToString(3) ?? "1.0.0");
        PreviewKeyDown += OnKey;
        NavDashboard.IsChecked = true;
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb) ShowPage((string)rb.Tag);
    }

    /// <summary>Opens a page by name. Each visit creates a fresh page so data is always current.</summary>
    public void Navigate(string page)
    {
        var rb = page switch
        {
            "Dashboard" => NavDashboard, "NewSale" => NavSale, "Sales" => NavSales, "Customers" => NavCustomers,
            "Stock" => NavStock, "Purchases" => NavPurchases, "Suppliers" => NavSuppliers, "Models" => NavModels,
            "Settings" => NavSettings, _ => NavDashboard
        };
        if (rb.IsChecked == true) ShowPage(page); else rb.IsChecked = true;
    }

    private bool _suppressNav;

    /// <summary>Shows a ready-made page (e.g. Sales History filtered for one customer).</summary>
    public void PageHostShow(UserControl view)
    {
        _suppressNav = true;
        if (view is SalesHistoryView) NavSales.IsChecked = true;
        else if (view is NewSaleView) NavSale.IsChecked = true;
        _suppressNav = false;
        PageHost.Content = view;
    }

    private void ShowPage(string page)
    {
        if (_suppressNav) return;
        PageHost.Content = page switch
        {
            "NewSale" => new NewSaleView(),
            "Sales" => new SalesHistoryView(),
            "Customers" => new CustomersView(),
            "Stock" => new StockView(),
            "Purchases" => new PurchasesView(),
            "Suppliers" => new SuppliersView(),
            "Models" => new ModelsView(),
            "Settings" => new SettingsView(),
            _ => new DashboardView()
        };
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        var page = e.Key switch
        {
            Key.F1 => "Dashboard", Key.F2 => "NewSale", Key.F3 => "Sales", Key.F4 => "Customers",
            Key.F5 => "Stock", Key.F6 => "Purchases", _ => null
        };
        if (page != null && OwnedWindows.Count == 0)
        {
            Navigate(page);
            e.Handled = true;
        }
    }
}
