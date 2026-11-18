using BatteryShop.Helpers;

namespace BatteryShop.Models;

// ---------------------------------------------------------------- Purchases

/// <summary>Editable line on the New Purchase screen.</summary>
public class PurchaseLine : ObservableObject
{
    private BatteryModel _model;
    private long _quantity = 1;
    private long _unitCost;
    private string _serials = "";

    public BatteryModel Model { get => _model; set => Set(ref _model, value); }
    public long Quantity { get => _quantity; set { if (Set(ref _quantity, value)) OnPropertyChanged(nameof(LineTotal)); } }
    public long UnitCost { get => _unitCost; set { if (Set(ref _unitCost, value)) OnPropertyChanged(nameof(LineTotal)); } }
    /// <summary>Serial numbers separated by comma, space or new line (optional).</summary>
    public string Serials { get => _serials; set => Set(ref _serials, value); }
    public long LineTotal => Quantity * UnitCost;

    public List<string> SerialList() => (Serials ?? "")
        .Split(new[] { ',', ' ', '\n', '\r', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
}

public class PurchaseRow
{
    public long Id { get; set; }
    public string PurchaseNo { get; set; }
    public string SupplierInvoiceNo { get; set; }
    public string PurchaseDate { get; set; }
    public string SupplierName { get; set; }
    public long Items { get; set; }
    public long NetTotal { get; set; }
    public string Status { get; set; }
}

// ---------------------------------------------------------------- Sales

/// <summary>Editable battery line on the New Sale screen.</summary>
public class SaleLine : ObservableObject
{
    private long _unitPrice;
    private long _discount;
    private long _warrantyMonths;

    public BatteryUnit Unit { get; set; }
    public string Description => Unit?.ModelDisplay;
    public string SerialNo => Unit?.SerialNo;
    public string Condition => Unit?.Condition;

    public long UnitPrice { get => _unitPrice; set { if (Set(ref _unitPrice, value)) OnPropertyChanged(nameof(LineTotal)); } }
    public long Discount { get => _discount; set { if (Set(ref _discount, value)) OnPropertyChanged(nameof(LineTotal)); } }
    public long WarrantyMonths { get => _warrantyMonths; set => Set(ref _warrantyMonths, value); }
    public long LineTotal => UnitPrice - Discount;
}

/// <summary>Old battery taken from the customer.</summary>
public class ExchangeLine : ObservableObject
{
    private string _description = "Old battery";
    private double? _weightKg;
    private long _exchangeValue;

    public string Description { get => _description; set => Set(ref _description, value); }
    public double? WeightKg { get => _weightKg; set => Set(ref _weightKg, value); }
    public long ExchangeValue { get => _exchangeValue; set => Set(ref _exchangeValue, value); }
}

/// <summary>Row from vw_SaleSummary (sales history and receipts).</summary>
public class SaleSummary
{
    public long SaleId { get; set; }
    public string InvoiceNo { get; set; }
    public string SaleDate { get; set; }
    public long CustomerId { get; set; }
    public string CustomerName { get; set; }
    public string Phone { get; set; }
    public long SubTotal { get; set; }
    public long Discount { get; set; }
    public long ExchangeTotal { get; set; }
    public long NetTotal { get; set; }
    public string PaymentType { get; set; }
    public string Status { get; set; }
    public long PaidAmount { get; set; }
    public long RemainingAmount { get; set; }
    public long GrossProfit { get; set; }
}

public class SaleItemRow
{
    public string Description { get; set; }
    public string SerialNo { get; set; }
    public string Condition { get; set; }
    public long UnitPrice { get; set; }
    public long Discount { get; set; }
    public long LineTotal { get; set; }
    public long WarrantyMonths { get; set; }
    public string WarrantyExpiry { get; set; }
}

public class SaleExchangeRow
{
    public string Description { get; set; }
    public double? WeightKg { get; set; }
    public long ExchangeValue { get; set; }
}

public class PaymentRow
{
    public long Id { get; set; }
    public string ReceiptNo { get; set; }
    public string PaymentDate { get; set; }
    public long CustomerId { get; set; }
    public string CustomerName { get; set; }
    public string InvoiceNo { get; set; }
    public long Amount { get; set; }
    public string Method { get; set; }
    public string Notes { get; set; }
    public string Status { get; set; }
}

/// <summary>Everything needed to print a sale receipt.</summary>
public class SaleReceipt
{
    public SaleSummary Sale { get; set; }
    public Customer Customer { get; set; }
    public List<SaleItemRow> Items { get; set; } = new();
    public List<SaleExchangeRow> Exchanges { get; set; } = new();
    public List<PaymentRow> Payments { get; set; } = new();
    /// <summary>Customer's total balance across all bills, after this sale.</summary>
    public long CustomerTotalBalance { get; set; }
    public long PrintCount { get; set; }
}

public class DashboardData
{
    public long TodaySales { get; set; }
    public long TodayBills { get; set; }
    public long TodayCollection { get; set; }
    public long TodayProfit { get; set; }
    public long MonthSales { get; set; }
    public long MonthProfit { get; set; }
    public long CustomerReceivable { get; set; }
    public long SupplierPayable { get; set; }
    public long NewStockQty { get; set; }
    public long SecondHandStockQty { get; set; }
    public long StockValue { get; set; }
    public long PendingExchanges { get; set; }
    public List<ModelStock> LowStock { get; set; } = new();
}

public static class PaymentMethods
{
    public static readonly string[] All = { "Cash", "Bank", "Cheque", "Mobile Wallet" };
}
