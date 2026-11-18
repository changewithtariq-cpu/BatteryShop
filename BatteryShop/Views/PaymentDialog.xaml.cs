using System.Windows;
using BatteryShop.Helpers;
using BatteryShop.Models;

namespace BatteryShop.Views;

/// <summary>Asks for a payment amount. Used for "Receive from customer" and "Pay supplier".</summary>
public partial class PaymentDialog : Window
{
    public long Amount { get; private set; }
    public string Method => MethodBox.SelectedItem as string ?? "Cash";
    public string Notes => NotesBox.Text.Trim();
    public DateTime Date => DateBox.SelectedDate ?? DateTime.Today;
    public bool PrintReceipt => PrintBox.IsChecked == true;

    public PaymentDialog(string title, string partyName, long currentBalance, string balanceLabel, bool showPrint)
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
        Title = title;
        PartyText.Text = partyName;
        BalanceText.Text = $"{balanceLabel}: {UI.Rs(currentBalance)}";
        MethodBox.ItemsSource = PaymentMethods.All;
        MethodBox.SelectedIndex = 0;
        DateBox.SelectedDate = DateTime.Today;
        PrintBox.Visibility = showPrint ? Visibility.Visible : Visibility.Collapsed;
        if (currentBalance > 0) AmountBox.Text = currentBalance.ToString("N0");
        Loaded += (_, _) => { AmountBox.Focus(); AmountBox.SelectAll(); };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        UI.Run(() =>
        {
            Amount = UI.Positive(AmountBox.Text, "Amount");
            if (Date > DateTime.Today) throw new Data.AppException("Date cannot be in the future.");
            DialogResult = true;
        });
    }
}
