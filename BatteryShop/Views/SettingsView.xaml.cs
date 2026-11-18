using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using BatteryShop.Data;
using BatteryShop.Helpers;
using BatteryShop.Printing;
using BatteryShop.Services;
using Microsoft.Win32;

namespace BatteryShop.Views;

public partial class SettingsView : UserControl
{
    private const string NoPrinter = "(Ask every time)";

    public SettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
    }

    private void Load()
    {
        var s = CatalogRepo.GetSettings();
        ShopName.Text = s.ShopName;
        Address.Text = s.Address;
        Phone.Text = s.Phone;
        Footer.Text = s.ReceiptFooter;
        BackupFolder.Text = string.IsNullOrWhiteSpace(s.BackupFolder) ? BackupService.DefaultFolder : s.BackupFolder;

        var printers = new List<string> { NoPrinter };
        printers.AddRange(ReceiptPrinter.InstalledPrinters());
        PrinterBox.ItemsSource = printers;
        PrinterBox.SelectedItem = printers.Contains(s.ReceiptPrinterName ?? "") ? s.ReceiptPrinterName : NoPrinter;

        LastBackupText.Text = "Last backup: " + (string.IsNullOrEmpty(s.LastBackupAt) ? "never" : UI.FormatDateTime(s.LastBackupAt));
        DbPathText.Text = Db.DbPath;
    }

    private void SaveSettings()
    {
        var s = CatalogRepo.GetSettings();
        if (string.IsNullOrWhiteSpace(ShopName.Text)) throw new AppException("Shop name is required.");
        s.ShopName = ShopName.Text.Trim();
        s.Address = Address.Text.Trim();
        s.Phone = Phone.Text.Trim();
        s.ReceiptFooter = Footer.Text.Trim();
        s.ReceiptPrinterName = PrinterBox.SelectedItem as string == NoPrinter ? null : PrinterBox.SelectedItem as string;
        s.BackupFolder = string.IsNullOrWhiteSpace(BackupFolder.Text) ? null : BackupFolder.Text.Trim();
        CatalogRepo.SaveSettings(s);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (UI.Run(SaveSettings)) UI.Info("Settings saved.");
    }

    private void TestPrint_Click(object sender, RoutedEventArgs e)
    {
        UI.Run(() =>
        {
            SaveSettings();
            ReceiptPrinter.PrintOrPreview(ReceiptBuilder.BuildTestPage(), null);
        });
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Choose backup folder" };
        if (Directory.Exists(BackupFolder.Text)) dlg.InitialDirectory = BackupFolder.Text;
        if (dlg.ShowDialog() == true) BackupFolder.Text = dlg.FolderName;
    }

    private void BackupNow_Click(object sender, RoutedEventArgs e)
    {
        UI.Run(() =>
        {
            SaveSettings();
            var file = BackupService.BackupNow();
            UI.Info("Backup saved:\n" + file);
            Load();
        });
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        UI.Run(() =>
        {
            var folder = BackupService.GetFolder();
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        });
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choose a backup to restore",
            Filter = "Shop backup (*.db)|*.db",
            InitialDirectory = Directory.Exists(BackupService.GetFolder()) ? BackupService.GetFolder() : ""
        };
        if (dlg.ShowDialog() != true) return;
        if (!UI.Confirm($"Restore from:\n{dlg.FileName}\n\nAll current data will be replaced by this backup. Continue?")) return;

        if (UI.Run(() => BackupService.Restore(dlg.FileName)))
        {
            UI.Info("Restore complete. The app will now close. Please open it again.");
            Application.Current.Shutdown();
        }
    }
}
