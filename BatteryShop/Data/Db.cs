using System.Globalization;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace BatteryShop.Data;

/// <summary>
/// Opens the SQLite database, creates it on first run, and provides small
/// helper methods (Query / Execute / Scalar) so repositories stay short.
/// </summary>
public static class Db
{
    public const string DateFormat = "yyyy-MM-dd HH:mm:ss";
    public const int SchemaVersion = 1;

    public static string DataFolder { get; private set; }
    public static string DbPath { get; private set; }

    public static void Initialize(string dataFolder = null)
    {
        DataFolder = dataFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AyazBatteryShop");
        Directory.CreateDirectory(DataFolder);
        DbPath = Path.Combine(DataFolder, "shop.db");

        using var conn = Open();
        Exec(conn, "PRAGMA journal_mode = WAL;");
        var version = Convert.ToInt32(Scalar(conn, "PRAGMA user_version;"));
        if (version == 0)
        {
            using var tx = conn.BeginTransaction();
            Exec(conn, ReadSchemaScript(), null, tx);
            Exec(conn, $"PRAGMA user_version = {SchemaVersion};", null, tx);
            tx.Commit();
        }
        // Future schema upgrades go here: if (version < 2) { ... }
    }

    private static string ReadSchemaScript()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames().First(n => n.EndsWith("Schema.sql"));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    public static string Now() => DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture);
    public static string ToDb(DateTime d) => d.ToString(DateFormat, CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------

    private static SqliteCommand Cmd(SqliteConnection conn, string sql, object args, SqliteTransaction tx)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (tx != null) cmd.Transaction = tx;
        if (args != null)
        {
            foreach (var p in args.GetType().GetProperties())
                cmd.Parameters.AddWithValue("@" + p.Name, ToDbValue(p.GetValue(args)));
        }
        return cmd;
    }

    private static object ToDbValue(object v) => v switch
    {
        null => DBNull.Value,
        DateTime d => ToDb(d),
        bool b => b ? 1L : 0L,
        Enum e => e.ToString(),
        _ => v
    };

    public static int Exec(SqliteConnection conn, string sql, object args = null, SqliteTransaction tx = null)
    {
        using var cmd = Cmd(conn, sql, args, tx);
        return cmd.ExecuteNonQuery();
    }

    public static object Scalar(SqliteConnection conn, string sql, object args = null, SqliteTransaction tx = null)
    {
        using var cmd = Cmd(conn, sql, args, tx);
        var r = cmd.ExecuteScalar();
        return r is DBNull ? null : r;
    }

    public static long ScalarLong(SqliteConnection conn, string sql, object args = null, SqliteTransaction tx = null)
        => Convert.ToInt64(Scalar(conn, sql, args, tx) ?? 0L);

    /// <summary>Inserts a row and returns the new Id.</summary>
    public static long Insert(SqliteConnection conn, string sql, object args = null, SqliteTransaction tx = null)
    {
        Exec(conn, sql, args, tx);
        return ScalarLong(conn, "SELECT last_insert_rowid();", null, tx);
    }

    /// <summary>Runs a SELECT and maps each row to T by matching column names to property names.</summary>
    public static List<T> Query<T>(SqliteConnection conn, string sql, object args = null, SqliteTransaction tx = null) where T : new()
    {
        using var cmd = Cmd(conn, sql, args, tx);
        using var rd = cmd.ExecuteReader();
        var props = typeof(T).GetProperties().Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var map = new PropertyInfo[rd.FieldCount];
        for (int i = 0; i < rd.FieldCount; i++)
            props.TryGetValue(rd.GetName(i), out map[i]);

        var list = new List<T>();
        while (rd.Read())
        {
            var item = new T();
            for (int i = 0; i < map.Length; i++)
            {
                if (map[i] == null || rd.IsDBNull(i)) continue;
                map[i].SetValue(item, ChangeType(rd.GetValue(i), map[i].PropertyType));
            }
            list.Add(item);
        }
        return list;
    }

    public static T QueryFirst<T>(SqliteConnection conn, string sql, object args = null, SqliteTransaction tx = null) where T : new()
        => Query<T>(conn, sql, args, tx).FirstOrDefault();

    private static object ChangeType(object value, Type target)
    {
        var t = Nullable.GetUnderlyingType(target) ?? target;
        if (t.IsInstanceOfType(value)) return value;
        if (t == typeof(DateTime))
            return DateTime.Parse(value.ToString()!, CultureInfo.InvariantCulture);
        if (t == typeof(bool)) return System.Convert.ToInt64(value) != 0;
        if (t.IsEnum) return Enum.Parse(t, value.ToString()!);
        if (t == typeof(string)) return System.Convert.ToString(value, CultureInfo.InvariantCulture);
        return System.Convert.ChangeType(value, t, CultureInfo.InvariantCulture);
    }

    /// <summary>Gets the next document number, e.g. SAL-000123. Call inside the same transaction as the insert.</summary>
    public static string NextNumber(SqliteConnection conn, SqliteTransaction tx, string name)
    {
        var row = QueryFirst<SeqRow>(conn, "SELECT Prefix, NextValue FROM NumberSequences WHERE Name=@name", new { name }, tx)
                  ?? throw new InvalidOperationException("Missing number sequence: " + name);
        Exec(conn, "UPDATE NumberSequences SET NextValue = NextValue + 1 WHERE Name=@name", new { name }, tx);
        return row.Prefix + row.NextValue.ToString("D6");
    }

    private class SeqRow { public string Prefix { get; set; } public long NextValue { get; set; } }
}
