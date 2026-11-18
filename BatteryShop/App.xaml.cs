using System.Windows;
using System.Windows.Threading;
using BatteryShop.Data;
using BatteryShop.Services;

namespace BatteryShop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;

        try
        {
            Db.Initialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not open the database.\n\n" + ex.Message, "Ayaz Battery", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        try { BackupService.AutoBackupIfDue(); } catch { /* backup problems are shown in Settings, never block start-up */ }

        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { BackupService.AutoBackupIfDue(); } catch { }
        base.OnExit(e);
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Helpers.UI.Error(e.Exception);
        e.Handled = true;
    }
}
