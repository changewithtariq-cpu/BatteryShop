using System.Windows;
using System.Windows.Controls;
using BatteryShop.Data;
using BatteryShop.Helpers;
using BatteryShop.Models;

namespace BatteryShop.Views;

public partial class SuppliersView : UserControl
{
    private Supplier _editing;

    public SuppliersView()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
    }

    private void Load()
    {
        var list = PartyRepo.GetSuppliers(activeOnly: ShowInactive.IsChecked != true);
        SupplierGrid.ItemsSource = list;
        TotalText.Text = "Total you owe: " + UI.Rs(list.Where(s => s.Balance > 0).Sum(s => s.Balance));
    }

    private void Reload(object sender, RoutedEventArgs e) => Load();

    private void SupplierGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SupplierGrid.SelectedItem is not Supplier s) return;
        _editing = s;
        FormTitle.Text = "Edit supplier";
        FName.Text = s.Name;
        FPhone.Text = s.Phone;
        FAddress.Text = s.Address;
        FOpening.Text = s.OpeningBalance.ToString("N0");
        FActive.IsChecked = s.IsActive;
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _editing = null;
        SupplierGrid.SelectedItem = null;
        FormTitle.Text = "New supplier";
        FName.Text = FPhone.Text = FAddress.Text = "";
        FOpening.Text = "0";
        FActive.IsChecked = true;
        FName.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        UI.Run(() =>
        {
            PartyRepo.SaveSupplier(new Supplier
            {
                Id = _editing?.Id ?? 0, Name = FName.Text, Phone = FPhone.Text.Trim(), Address = FAddress.Text.Trim(),
                OpeningBalance = UI.NonNegative(FOpening.Text, "Opening balance"), IsActive = FActive.IsChecked == true
            });
            Load();
            New_Click(null, null);
        });
    }

    private Supplier Selected()
    {
        if (SupplierGrid.SelectedItem is Supplier s) return s;
        UI.Info("Please select a supplier first.");
        return null;
    }

    private void Pay_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected();
        if (s == null) return;
        var dlg = new PaymentDialog("Pay supplier", s.Name, s.Balance, "You owe", showPrint: false);
        if (dlg.ShowDialog() != true) return;
        UI.Run(() =>
        {
            var no = PartyRepo.PaySupplier(s.Id, dlg.Amount, dlg.Method, dlg.Notes, null, dlg.Date);
            UI.Info($"Payment {no} of {UI.Rs(dlg.Amount)} saved.");
            Load();
        });
    }

    private void Statement_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected();
        if (s == null) return;
        new LedgerWindow(s.Name, "Supplier statement" + (string.IsNullOrEmpty(s.Phone) ? "" : "  |  " + s.Phone),
            "Purchase", "Paid", "You owe", (f, t) => PartyRepo.GetSupplierLedger(s.Id, f, t)).ShowDialog();
        Load();
    }
}
