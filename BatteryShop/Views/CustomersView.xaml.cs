using System.Windows;
using System.Windows.Controls;
using BatteryShop.Data;
using BatteryShop.Helpers;
using BatteryShop.Models;
using BatteryShop.Printing;

namespace BatteryShop.Views;

public partial class CustomersView : UserControl
{
    private Customer _editing;

    public CustomersView()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
    }

    private void Load()
    {
        var list = PartyRepo.GetCustomers(SearchBox.Text, OnlyBalance.IsChecked == true);
        CustomerGrid.ItemsSource = list;
        TotalText.Text = $"{list.Count} customer(s)   |   Total receivable: {UI.Rs(list.Where(c => c.Balance > 0).Sum(c => c.Balance))}";
    }

    private void Reload(object sender, RoutedEventArgs e) => Load();
    private void Search_TextChanged(object sender, TextChangedEventArgs e) { if (IsLoaded) Load(); }

    private void CustomerGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CustomerGrid.SelectedItem is not Customer c) return;
        _editing = c;
        FormTitle.Text = c.IsWalkIn ? "Walk-in customer (cash only)" : "Edit customer";
        FName.Text = c.Name;
        FPhone.Text = c.Phone;
        FAddress.Text = c.Address;
        FVehicle.Text = c.VehicleInfo;
        FOpening.Text = c.OpeningBalance.ToString("N0");
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _editing = null;
        CustomerGrid.SelectedItem = null;
        FormTitle.Text = "New customer";
        FName.Text = FPhone.Text = FAddress.Text = FVehicle.Text = "";
        FOpening.Text = "0";
        FName.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        UI.Run(() =>
        {
            if (_editing?.IsWalkIn == true) throw new AppException("The walk-in customer cannot be edited.");
            PartyRepo.SaveCustomer(new Customer
            {
                Id = _editing?.Id ?? 0, Name = FName.Text, Phone = FPhone.Text, Address = FAddress.Text.Trim(),
                VehicleInfo = FVehicle.Text.Trim(), OpeningBalance = UI.NonNegative(FOpening.Text, "Opening balance")
            });
            Load();
            New_Click(null, null);
        });
    }

    private Customer Selected()
    {
        if (CustomerGrid.SelectedItem is Customer c) return c;
        UI.Info("Please select a customer first.");
        return null;
    }

    private void Receive_Click(object sender, RoutedEventArgs e)
    {
        var c = Selected();
        if (c == null) return;
        if (c.IsWalkIn) { UI.Info("Walk-in customer has no account. Select a named customer."); return; }
        var dlg = new PaymentDialog("Receive payment", c.Display, c.Balance, "Current balance", showPrint: true);
        if (dlg.ShowDialog() != true) return;
        UI.Run(() =>
        {
            var p = PartyRepo.ReceivePayment(c.Id, dlg.Amount, dlg.Method, dlg.Notes, dlg.Date);
            var newBalance = PartyRepo.GetCustomer(c.Id).Balance;
            if (dlg.PrintReceipt)
                ReceiptPrinter.PreviewAndPrint(ReceiptBuilder.BuildPaymentReceipt(p, c, c.Balance, newBalance), null);
            else
                UI.Info($"Payment {p.ReceiptNo} of {UI.Rs(p.Amount)} saved.\nNew balance: {UI.Rs(newBalance)}");
            Load();
        });
    }

    private void Statement_Click(object sender, RoutedEventArgs e)
    {
        var c = Selected();
        if (c == null) return;
        new LedgerWindow(c.Name, "Customer account statement" + (string.IsNullOrEmpty(c.Phone) ? "" : "  |  " + c.Phone),
            "Bill", "Received", "Balance due", (f, t) => PartyRepo.GetCustomerLedger(c.Id, f, t)).ShowDialog();
        Load();
    }

    private void Bills_Click(object sender, RoutedEventArgs e)
    {
        var c = Selected();
        if (c == null) return;
        MainWindow.Instance.PageHostShow(new SalesHistoryView(c));
    }
}
