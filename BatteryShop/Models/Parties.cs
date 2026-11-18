namespace BatteryShop.Models;

public class Customer
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string Phone { get; set; }
    public string Address { get; set; }
    public string VehicleInfo { get; set; }
    public long OpeningBalance { get; set; }
    public bool IsWalkIn { get; set; }
    public bool IsActive { get; set; } = true;
    public long Balance { get; set; }

    public string Display => string.IsNullOrWhiteSpace(Phone) ? Name : $"{Name}  ({Phone})";
    public override string ToString() => Display;
}

public class Supplier
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string Phone { get; set; }
    public string Address { get; set; }
    public long OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public long Balance { get; set; }
    public override string ToString() => Name;
}

/// <summary>One line of a customer or supplier account statement.</summary>
public class LedgerRow
{
    public string TxnDate { get; set; }
    public string TxnType { get; set; }
    public string RefNo { get; set; }
    public long Debit { get; set; }
    public long Credit { get; set; }
    public long Balance { get; set; }
}
