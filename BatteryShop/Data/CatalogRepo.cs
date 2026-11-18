using BatteryShop.Models;

namespace BatteryShop.Data;

/// <summary>Brands, battery models, shop settings.</summary>
public static class CatalogRepo
{
    // ---------------------------------------------------------------- Settings
    public static ShopSettings GetSettings()
    {
        using var c = Db.Open();
        return Db.QueryFirst<ShopSettings>(c, "SELECT * FROM ShopSettings WHERE Id = 1") ?? new ShopSettings();
    }

    public static void SaveSettings(ShopSettings s)
    {
        using var c = Db.Open();
        Db.Exec(c, @"UPDATE ShopSettings SET ShopName=@ShopName, Address=@Address, Phone=@Phone, LogoPath=@LogoPath,
                     ReceiptFooter=@ReceiptFooter, ReceiptPrinterName=@ReceiptPrinterName, BackupFolder=@BackupFolder
                     WHERE Id = 1", s);
    }

    public static void SetLastBackup(string when)
    {
        using var c = Db.Open();
        Db.Exec(c, "UPDATE ShopSettings SET LastBackupAt=@when WHERE Id=1", new { when });
    }

    // ---------------------------------------------------------------- Brands
    public static List<Brand> GetBrands(bool activeOnly = true)
    {
        using var c = Db.Open();
        return Db.Query<Brand>(c, $"SELECT * FROM Brands {(activeOnly ? "WHERE IsActive=1" : "")} ORDER BY Name");
    }

    public static long SaveBrand(Brand b)
    {
        if (string.IsNullOrWhiteSpace(b.Name)) throw new AppException("Brand name is required.");
        using var c = Db.Open();
        if (Db.ScalarLong(c, "SELECT COUNT(*) FROM Brands WHERE Name=@Name COLLATE NOCASE AND Id<>@Id", new { b.Name, b.Id }) > 0)
            throw new AppException($"Brand '{b.Name}' already exists.");
        if (b.Id == 0)
            return b.Id = Db.Insert(c, "INSERT INTO Brands(Name, IsActive) VALUES(@Name, @IsActive)", new { Name = b.Name.Trim(), b.IsActive });
        Db.Exec(c, "UPDATE Brands SET Name=@Name, IsActive=@IsActive WHERE Id=@Id", new { Name = b.Name.Trim(), b.IsActive, b.Id });
        return b.Id;
    }

    // ---------------------------------------------------------------- Models
    public static List<BatteryModel> GetModels(bool activeOnly = true)
    {
        using var c = Db.Open();
        return Db.Query<BatteryModel>(c, $@"
            SELECT m.*, b.Name AS BrandName
            FROM BatteryModels m JOIN Brands b ON b.Id = m.BrandId
            {(activeOnly ? "WHERE m.IsActive = 1" : "")}
            ORDER BY b.Name, m.ModelName, m.CapacityAh");
    }

    public static long SaveModel(BatteryModel m)
    {
        if (m.BrandId == 0) throw new AppException("Please select a brand.");
        if (string.IsNullOrWhiteSpace(m.ModelName)) throw new AppException("Model name is required.");
        if (m.CapacityAh <= 0) throw new AppException("Capacity (Ah) must be greater than 0.");
        if (m.Voltage <= 0) throw new AppException("Voltage must be greater than 0.");
        if (m.DefaultSalePrice < 0 || m.WarrantyMonths < 0 || m.LowStockLevel < 0)
            throw new AppException("Price, warranty and low stock alert cannot be negative.");
        if (m.WarrantyMonths > 120) throw new AppException("Warranty cannot be more than 120 months.");
        if (m.ApproxWeightKg.HasValue && m.ApproxWeightKg <= 0) throw new AppException("Weight must be greater than 0 (or leave it empty).");
        m.ModelName = m.ModelName.Trim();

        using var c = Db.Open();
        if (Db.ScalarLong(c, @"SELECT COUNT(*) FROM BatteryModels
                               WHERE BrandId=@BrandId AND ModelName=@ModelName COLLATE NOCASE AND CapacityAh=@CapacityAh AND Id<>@Id", m) > 0)
            throw new AppException("This brand / model / capacity already exists.");

        var args = new
        {
            m.Id, m.BrandId, m.ModelName, m.CapacityAh, m.Voltage, m.BatteryType, m.ApproxWeightKg,
            m.DefaultSalePrice, m.WarrantyMonths, m.LowStockLevel, m.IsActive
        };
        if (m.Id == 0)
            return m.Id = Db.Insert(c, @"INSERT INTO BatteryModels
                (BrandId, ModelName, CapacityAh, Voltage, BatteryType, ApproxWeightKg, DefaultSalePrice, WarrantyMonths, LowStockLevel, IsActive)
                VALUES (@BrandId, @ModelName, @CapacityAh, @Voltage, @BatteryType, @ApproxWeightKg, @DefaultSalePrice, @WarrantyMonths, @LowStockLevel, @IsActive)", args);

        Db.Exec(c, @"UPDATE BatteryModels SET BrandId=@BrandId, ModelName=@ModelName, CapacityAh=@CapacityAh, Voltage=@Voltage,
                     BatteryType=@BatteryType, ApproxWeightKg=@ApproxWeightKg, DefaultSalePrice=@DefaultSalePrice,
                     WarrantyMonths=@WarrantyMonths, LowStockLevel=@LowStockLevel, IsActive=@IsActive WHERE Id=@Id", args);
        return m.Id;
    }
}

/// <summary>A business-rule error with a message that can be shown to the user as-is.</summary>
public class AppException : Exception
{
    public AppException(string message) : base(message) { }
}
