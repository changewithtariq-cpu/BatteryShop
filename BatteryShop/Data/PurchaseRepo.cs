using BatteryShop.Models;

namespace BatteryShop.Data;

public static class PurchaseRepo
{
    /// <summary>
    /// Saves a purchase bill: header, lines, one BatteryUnit per battery received,
    /// stock movements, and (optionally) a payment to the supplier. All in one transaction.
    /// </summary>
    public static string SavePurchase(long supplierId, string supplierInvoiceNo, DateTime date, IList<PurchaseLine> lines,
                                      long discount, long paidNow, string payMethod, string notes)
    {
        if (supplierId == 0) throw new AppException("Please select a supplier.");
        var valid = lines.Where(l => l.Model != null).ToList();
        if (valid.Count == 0) throw new AppException("Add at least one battery line.");
        foreach (var l in valid)
        {
            if (l.Quantity <= 0) throw new AppException($"Quantity must be at least 1 for {l.Model.DisplayName}.");
            if (l.UnitCost <= 0) throw new AppException($"Cost each is required for {l.Model.DisplayName} and must be greater than 0.");
            if (l.SerialList().Count > l.Quantity)
                throw new AppException($"{l.Model.DisplayName}: {l.SerialList().Count} serial numbers entered but quantity is {l.Quantity}.");
        }
        var subTotal = valid.Sum(l => l.LineTotal);
        if (discount < 0 || discount > subTotal) throw new AppException("Discount is not valid.");
        var net = subTotal - discount;
        if (paidNow < 0 || paidNow > net) throw new AppException("Paid amount cannot be more than the bill total.");

        // Spread the bill discount over units so each battery's cost is accurate for profit.
        double factor = subTotal == 0 ? 1 : (double)net / subTotal;

        var when = date.Date == DateTime.Today ? DateTime.Now : date.Date.AddHours(12);
        var dateStr = Db.ToDb(when);

        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        StockRepo.EnsureSerialsFree(c, tx, valid.SelectMany(l => l.SerialList()));

        var no = Db.NextNumber(c, tx, "Purchase");
        var purchaseId = Db.Insert(c, @"INSERT INTO Purchases (PurchaseNo, SupplierId, SupplierInvoiceNo, PurchaseDate, SubTotal, Discount, NetTotal, Notes)
                                        VALUES (@no, @supplierId, @supplierInvoiceNo, @dateStr, @subTotal, @discount, @net, @notes)",
            new { no, supplierId, supplierInvoiceNo, dateStr, subTotal, discount, net, notes }, tx);

        foreach (var l in valid)
        {
            var itemId = Db.Insert(c, @"INSERT INTO PurchaseItems (PurchaseId, ModelId, Quantity, UnitCost, LineTotal)
                                        VALUES (@purchaseId, @ModelId, @Quantity, @UnitCost, @LineTotal)",
                new { purchaseId, ModelId = l.Model.Id, l.Quantity, l.UnitCost, l.LineTotal }, tx);

            var serials = l.SerialList();
            var unitCost = (long)Math.Round(l.UnitCost * factor);
            for (int i = 0; i < l.Quantity; i++)
            {
                StockRepo.InsertUnit(c, tx, new BatteryUnit
                {
                    ModelId = l.Model.Id, SerialNo = i < serials.Count ? serials[i] : null,
                    Condition = "New", Source = "Purchase", UnitCost = unitCost
                }, itemId, "PurchaseIn", "Purchases", purchaseId, dateStr);
            }
        }

        if (paidNow > 0)
            PartyRepo.PaySupplier(c, tx, supplierId, paidNow, payMethod ?? "Cash", "Paid with " + no, purchaseId, dateStr);

        tx.Commit();
        return no;
    }

    public static List<PurchaseRow> GetPurchases(DateTime from, DateTime to)
    {
        using var c = Db.Open();
        return Db.Query<PurchaseRow>(c, @"
            SELECT p.Id, p.PurchaseNo, p.SupplierInvoiceNo, p.PurchaseDate, s.Name AS SupplierName, p.NetTotal, p.Status,
                   (SELECT SUM(Quantity) FROM PurchaseItems i WHERE i.PurchaseId = p.Id) AS Items
            FROM Purchases p JOIN Suppliers s ON s.Id = p.SupplierId
            WHERE p.PurchaseDate BETWEEN @f AND @t
            ORDER BY p.PurchaseDate DESC, p.Id DESC",
            new { f = Db.ToDb(from.Date), t = Db.ToDb(to.Date.AddDays(1).AddSeconds(-1)) });
    }

    /// <summary>Cancels a purchase. Only allowed if none of its batteries have been sold yet.</summary>
    public static void VoidPurchase(long purchaseId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new AppException("Please give a reason.");
        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        var notInStock = Db.ScalarLong(c, @"SELECT COUNT(*) FROM BatteryUnits u JOIN PurchaseItems i ON i.Id = u.PurchaseItemId
                                            WHERE i.PurchaseId=@purchaseId AND u.Status <> 'InStock'", new { purchaseId }, tx);
        if (notInStock > 0)
            throw new AppException("Some batteries from this purchase are already sold or removed. It cannot be cancelled.");

        var n = Db.Exec(c, "UPDATE Purchases SET Status='Voided', VoidReason=@reason WHERE Id=@purchaseId AND Status='Active'",
            new { purchaseId, reason }, tx);
        if (n == 0) throw new AppException("Purchase not found or already cancelled.");

        var now = Db.Now();
        var unitIds = Db.Query<IdRow>(c, @"SELECT u.Id FROM BatteryUnits u JOIN PurchaseItems i ON i.Id = u.PurchaseItemId
                                           WHERE i.PurchaseId=@purchaseId", new { purchaseId }, tx);
        foreach (var u in unitIds)
        {
            Db.Exec(c, "UPDATE BatteryUnits SET Status='Removed' WHERE Id=@Id", u, tx);
            StockRepo.AddMovement(c, tx, u.Id, "AdjustOut", -1, "Purchases", purchaseId, now, "Purchase cancelled: " + reason);
        }
        // Payments made with this purchase are cancelled too
        Db.Exec(c, "UPDATE SupplierPayments SET Status='Voided' WHERE PurchaseId=@purchaseId", new { purchaseId }, tx);
        tx.Commit();
    }
}

public class IdRow { public long Id { get; set; } }
