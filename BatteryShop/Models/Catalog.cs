namespace BatteryShop.Models;

public class ShopSettings
{
    public long Id { get; set; } = 1;
    public string ShopName { get; set; }
    public string Address { get; set; }
    public string Phone { get; set; }
    public string LogoPath { get; set; }
    public string ReceiptFooter { get; set; }
    public string ReceiptPrinterName { get; set; }
    public string BackupFolder { get; set; }
    public string LastBackupAt { get; set; }
}

public class Brand
{
    public long Id { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public override string ToString() => Name;
}

public class BatteryModel
{
    public long Id { get; set; }
    public long BrandId { get; set; }
    public string BrandName { get; set; }
    public string ModelName { get; set; }
    public long CapacityAh { get; set; }
    public long Voltage { get; set; } = 12;
    public string BatteryType { get; set; } = "Lead Acid";
    public double? ApproxWeightKg { get; set; }
    public long DefaultSalePrice { get; set; }
    public long WarrantyMonths { get; set; }
    public long LowStockLevel { get; set; } = 2;
    public bool IsActive { get; set; } = true;

    public string DisplayName => $"{BrandName} {ModelName} {CapacityAh}Ah";
    public override string ToString() => DisplayName;

    public static readonly string[] Types = { "Lead Acid", "Dry/MF", "Tubular", "Lithium", "Other" };
}

public class ModelStock
{
    public long ModelId { get; set; }
    public string Brand { get; set; }
    public string ModelName { get; set; }
    public long CapacityAh { get; set; }
    public long LowStockLevel { get; set; }
    public long NewInStock { get; set; }
    public long SecondHandInStock { get; set; }
    public long StockValueAtCost { get; set; }
    public string DisplayName => $"{Brand} {ModelName} {CapacityAh}Ah";
    public bool IsLow => NewInStock <= LowStockLevel;
}

public class BatteryUnit
{
    public long Id { get; set; }
    public long ModelId { get; set; }
    public string ModelDisplay { get; set; }
    public string SerialNo { get; set; }
    public string DateCode { get; set; }
    public string Condition { get; set; } = "New";
    public string ConditionNote { get; set; }
    public string Source { get; set; }
    public long UnitCost { get; set; }
    public long? SalePrice { get; set; }
    public long DefaultSalePrice { get; set; }
    public long WarrantyMonths { get; set; }
    public string Status { get; set; }
    public string ReceivedDate { get; set; }

    /// <summary>Price to suggest on a sale: unit's own price (second-hand) or the model default.</summary>
    public long SuggestedPrice => SalePrice ?? DefaultSalePrice;
    public string Display => $"{ModelDisplay}  |  {(string.IsNullOrEmpty(SerialNo) ? "No serial" : SerialNo)}  |  {Condition}";
    public override string ToString() => Display;
}
