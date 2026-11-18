using BatteryShop.Models;
using Microsoft.Data.Sqlite;

namespace BatteryShop.Data;

/// <summary>Individual batteries (units) and stock movements.</summary>
public static class StockRepo
{
    private const string UnitSelect = @"
        SELECT u.*, b.Name || ' ' || m.ModelName || ' ' || m.CapacityAh || 'Ah' AS ModelDisplay,
               m.DefaultSalePrice, m.WarrantyMonths
        FROM BatteryUnits u
        JOIN BatteryModels m ON m.Id = u.ModelId
        JOIN Brands b        ON b.Id = m.BrandId ";

    public static List<ModelStock> GetModelStock()
    {
        using var c = Db.Open();
        return Db.Query<ModelStock>(c, "SELECT * FROM vw_ModelStock ORDER BY Brand, ModelName, CapacityAh");
    }

    /// <summary>Lists units. status/condition null = all. search matches serial or model text.</summary>
    public static List<BatteryUnit> GetUnits(long? modelId = null, string status = "InStock", string condition = null, string search = null)
    {
        using var c = Db.Open();
        return Db.Query<BatteryUnit>(c, UnitSelect + @"
            WHERE (@modelId IS NULL OR u.ModelId = @modelId)
              AND (@status IS NULL OR u.Status = @status)
              AND (@condition IS NULL OR u.Condition = @condition)
              AND (@search IS NULL OR u.SerialNo LIKE @search OR (b.Name || ' ' || m.ModelName || ' ' || m.CapacityAh) LIKE @search)
            ORDER BY b.Name, m.ModelName, u.ReceivedDate, u.Id",
            new { modelId, status, condition, search = string.IsNullOrWhiteSpace(search) ? null : "%" + search.Trim() + "%" });
    }

    public static BatteryUnit GetUnit(SqliteConnection c, long id, SqliteTransaction tx = null)
        => Db.QueryFirst<BatteryUnit>(c, UnitSelect + " WHERE u.Id = @id", new { id }, tx);

    /// <summary>Finds an in-stock unit by exact serial number (case-insensitive).</summary>
    public static BatteryUnit FindInStockBySerial(string serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return null;
        using var c = Db.Open();
        return Db.QueryFirst<BatteryUnit>(c, UnitSelect + " WHERE u.SerialNo = @serial AND u.Status = 'InStock'",
            new { serial = serial.Trim() });
    }

    public static void EnsureSerialsFree(SqliteConnection c, SqliteTransaction tx, IEnumerable<string> serials)
    {
        var list = serials.ToList();
        var dup = list.GroupBy(s => s, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (dup != null) throw new AppException($"Serial number '{dup.Key}' is entered more than once.");
        foreach (var s in list)
            if (Db.ScalarLong(c, "SELECT COUNT(*) FROM BatteryUnits WHERE SerialNo = @s", new { s }, tx) > 0)
                throw new AppException($"Serial number '{s}' already exists in the system.");
    }

    public static long InsertUnit(SqliteConnection c, SqliteTransaction tx, BatteryUnit u, long? purchaseItemId,
                                  string movementType, string refTable, long? refId, string date)
    {
        var id = Db.Insert(c, @"INSERT INTO BatteryUnits
              (ModelId, SerialNo, DateCode, Condition, ConditionNote, Source, PurchaseItemId, UnitCost, SalePrice, Status, ReceivedDate)
              VALUES (@ModelId, @SerialNo, @DateCode, @Condition, @ConditionNote, @Source, @purchaseItemId, @UnitCost, @SalePrice, 'InStock', @date)",
            new
            {
                u.ModelId, SerialNo = string.IsNullOrWhiteSpace(u.SerialNo) ? null : u.SerialNo.Trim(), u.DateCode,
                u.Condition, u.ConditionNote, u.Source, purchaseItemId, u.UnitCost, u.SalePrice, date
            }, tx);
        AddMovement(c, tx, id, movementType, +1, refTable, refId, date, null);
        return id;
    }

    public static void AddMovement(SqliteConnection c, SqliteTransaction tx, long unitId, string type, int direction,
                                   string refTable, long? refId, string date, string notes)
    {
        Db.Exec(c, @"INSERT INTO StockMovements (BatteryUnitId, MovementType, Direction, RefTable, RefId, MovementDate, Notes)
                     VALUES (@unitId, @type, @direction, @refTable, @refId, @date, @notes)",
            new { unitId, type, direction, refTable, refId, date, notes }, tx);
    }

    /// <summary>
    /// Adds stock without a purchase bill: opening stock when starting the system,
    /// or a second-hand battery bought / received outside a sale.
    /// </summary>
    public static int AddManualUnits(long modelId, string condition, string source, long quantity, List<string> serials,
                                     long unitCost, long? salePrice, string note)
    {
        if (modelId == 0) throw new AppException("Please select a battery model.");
        if (quantity <= 0) throw new AppException("Quantity must be at least 1.");
        if (unitCost <= 0) throw new AppException("Cost per battery must be greater than 0.");
        if (salePrice.HasValue && salePrice <= 0) throw new AppException("Sale price must be greater than 0.");
        if (serials.Count > quantity) throw new AppException($"You entered {serials.Count} serial numbers but quantity is {quantity}.");

        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        EnsureSerialsFree(c, tx, serials);
        var now = Db.Now();
        var type = source == "Opening" ? "OpeningIn" : "AdjustIn";
        for (int i = 0; i < quantity; i++)
        {
            InsertUnit(c, tx, new BatteryUnit
            {
                ModelId = modelId, SerialNo = i < serials.Count ? serials[i] : null, Condition = condition,
                ConditionNote = note, Source = source, UnitCost = unitCost, SalePrice = salePrice
            }, null, type, null, null, now);
        }
        tx.Commit();
        return (int)quantity;
    }

    /// <summary>Removes a unit from stock (damaged, lost, entered by mistake).</summary>
    public static void RemoveUnit(long unitId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new AppException("Please give a reason.");
        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        var n = Db.Exec(c, "UPDATE BatteryUnits SET Status='Removed' WHERE Id=@unitId AND Status='InStock'", new { unitId }, tx);
        if (n == 0) throw new AppException("This battery is not in stock.");
        AddMovement(c, tx, unitId, "AdjustOut", -1, null, null, Db.Now(), reason);
        tx.Commit();
    }

    public static void UpdateUnit(BatteryUnit u)
    {
        if (u.SalePrice.HasValue && u.SalePrice <= 0) throw new AppException("Sale price must be greater than 0.");
        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        var serial = string.IsNullOrWhiteSpace(u.SerialNo) ? null : u.SerialNo.Trim();
        if (serial != null && Db.ScalarLong(c, "SELECT COUNT(*) FROM BatteryUnits WHERE SerialNo=@serial AND Id<>@Id",
                new { serial, u.Id }, tx) > 0)
            throw new AppException($"Serial number '{serial}' already exists.");
        Db.Exec(c, @"UPDATE BatteryUnits SET SerialNo=@serial, DateCode=@DateCode, ConditionNote=@ConditionNote,
                     SalePrice=@SalePrice, UnitCost=@UnitCost WHERE Id=@Id",
            new { serial, u.DateCode, u.ConditionNote, u.SalePrice, u.UnitCost, u.Id }, tx);
        tx.Commit();
    }
}
