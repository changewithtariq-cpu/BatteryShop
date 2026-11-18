using BatteryShop.Data;
using Microsoft.Data.Sqlite;

namespace BatteryShop.Services;

/// <summary>
/// Database backup and restore. The whole shop is one file (shop.db),
/// so a backup is a safe copy of that file with the date in its name.
/// </summary>
public static class BackupService
{
    public const int KeepLast = 60;

    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AyazBatteryShop Backups");

    public static string GetFolder()
    {
        var s = CatalogRepo.GetSettings();
        return string.IsNullOrWhiteSpace(s.BackupFolder) ? DefaultFolder : s.BackupFolder;
    }

    /// <summary>Creates a backup and returns its full path.</summary>
    public static string BackupNow(string folder = null, string suffix = "")
    {
        folder ??= GetFolder();
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, $"AyazBattery_{DateTime.Now:yyyy-MM-dd_HHmmss}{suffix}.db");

        using (var src = Db.Open())
        using (var dst = new SqliteConnection($"Data Source={file}"))
        {
            dst.Open();
            src.BackupDatabase(dst);   // safe even while the app is running
        }
        SqliteConnection.ClearAllPools();
        CatalogRepo.SetLastBackup(Db.Now());
        Cleanup(folder);
        return file;
    }

    /// <summary>Makes one backup per day automatically (called at start-up and on exit).</summary>
    public static string AutoBackupIfDue()
    {
        var last = CatalogRepo.GetSettings().LastBackupAt;
        if (!string.IsNullOrEmpty(last) && last.StartsWith(DateTime.Today.ToString("yyyy-MM-dd"))) return null;
        return BackupNow();
    }

    private static void Cleanup(string folder)
    {
        var old = new DirectoryInfo(folder).GetFiles("AyazBattery_*.db")
            .Where(f => !f.Name.Contains("before-restore"))
            .OrderByDescending(f => f.CreationTime).Skip(KeepLast);
        foreach (var f in old)
        {
            try { f.Delete(); } catch { /* ignore locked files */ }
        }
    }

    /// <summary>
    /// Replaces the current database with a backup file. A safety backup of the
    /// current data is taken first. The app must be restarted afterwards.
    /// </summary>
    public static void Restore(string backupFile)
    {
        if (!File.Exists(backupFile)) throw new AppException("Backup file not found.");

        // Check that the file really is a shop database
        using (var test = new SqliteConnection($"Data Source={backupFile};Mode=ReadOnly"))
        {
            test.Open();
            using var cmd = test.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Sales'";
            if (Convert.ToInt64(cmd.ExecuteScalar()) == 0) throw new AppException("This file is not a valid shop backup.");
        }
        SqliteConnection.ClearAllPools();

        BackupNow(suffix: "_before-restore");
        SqliteConnection.ClearAllPools();

        foreach (var ext in new[] { "-wal", "-shm" })
            if (File.Exists(Db.DbPath + ext)) File.Delete(Db.DbPath + ext);
        File.Copy(backupFile, Db.DbPath, overwrite: true);
    }
}
