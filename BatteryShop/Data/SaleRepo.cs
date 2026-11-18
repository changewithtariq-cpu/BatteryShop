using BatteryShop.Models;

namespace BatteryShop.Data;

public static class SaleRepo
{
    /// <summary>
    /// Saves a sale: bill, batteries sold (marked Sold), old batteries exchanged,
    /// and the amount paid now. Returns the new sale Id.
    /// </summary>
    public static long SaveSale(long customerId, IList<SaleLine> lines, IList<ExchangeLine> exchanges,
                                long billDiscount, long paidNow, string payMethod, string notes)
    {
        if (customerId == 0) throw new AppException("Please select a customer.");
        if (lines.Count == 0) throw new AppException("Add at least one battery to the bill.");
        if (lines.Select(l => l.Unit.Id).Distinct().Count() != lines.Count)
            throw new AppException("The same battery is added twice.");
        foreach (var l in lines)
        {
            if (l.UnitPrice <= 0) throw new AppException($"Enter a price for {l.Description}.");
            if (l.Discount < 0 || l.Discount > l.UnitPrice) throw new AppException($"Discount is not valid for {l.Description}.");
            if (l.WarrantyMonths < 0) throw new AppException("Warranty months cannot be negative.");
        }
        var ex = exchanges.Where(e => e.ExchangeValue > 0 || !string.IsNullOrWhiteSpace(e.Description) && e.WeightKg > 0).ToList();
        if (ex.Any(e => e.ExchangeValue < 0)) throw new AppException("Exchange value cannot be negative.");

        var subTotal = lines.Sum(l => l.LineTotal);
        var exchangeTotal = ex.Sum(e => e.ExchangeValue);
        if (billDiscount < 0) throw new AppException("Discount cannot be negative.");
        var net = subTotal - billDiscount - exchangeTotal;
        if (net < 0) throw new AppException("Discount + exchange value is more than the bill amount.");
        if (paidNow < 0) throw new AppException("Paid amount cannot be negative.");
        if (paidNow > net) throw new AppException($"Paid amount (Rs. {paidNow:N0}) is more than the bill total (Rs. {net:N0}).");

        using var c = Db.Open();
        using var tx = c.BeginTransaction();

        var isWalkIn = Db.ScalarLong(c, "SELECT IsWalkIn FROM Customers WHERE Id=@customerId", new { customerId }, tx) == 1;
        if (isWalkIn && paidNow < net)
            throw new AppException("Walk-in customer must pay the full amount. Select or add a named customer for credit / partial payment.");

        var paymentType = paidNow >= net ? "Cash" : paidNow == 0 ? "Credit" : "Partial";
        var now = DateTime.Now;
        var nowStr = Db.ToDb(now);
        var invoiceNo = Db.NextNumber(c, tx, "Sale");

        var saleId = Db.Insert(c, @"
            INSERT INTO Sales (InvoiceNo, CustomerId, SaleDate, SubTotal, Discount, ExchangeTotal, NetTotal, PaymentType, Notes)
            VALUES (@invoiceNo, @customerId, @nowStr, @subTotal, @billDiscount, @exchangeTotal, @net, @paymentType, @notes)",
            new { invoiceNo, customerId, nowStr, subTotal, billDiscount, exchangeTotal, net, paymentType, notes }, tx);

        foreach (var l in lines)
        {
            // Re-read the unit inside the transaction so we never sell a battery twice
            var unit = StockRepo.GetUnit(c, l.Unit.Id, tx);
            if (unit == null || unit.Status != "InStock")
                throw new AppException($"Battery {l.Description} ({l.SerialNo ?? "no serial"}) is no longer in stock.");

            var expiry = l.WarrantyMonths > 0 ? Db.ToDb(now.Date.AddMonths((int)l.WarrantyMonths).AddDays(-1)) : null;
            Db.Exec(c, @"
                INSERT INTO SaleItems (SaleId, BatteryUnitId, ModelId, Condition, UnitPrice, Discount, LineTotal, CostAtSale,
                                       WarrantyMonths, WarrantyStart, WarrantyExpiry)
                VALUES (@saleId, @UnitId, @ModelId, @Condition, @UnitPrice, @Discount, @LineTotal, @UnitCost,
                        @WarrantyMonths, @start, @expiry)",
                new
                {
                    saleId, UnitId = unit.Id, unit.ModelId, unit.Condition, l.UnitPrice, l.Discount, l.LineTotal, unit.UnitCost,
                    l.WarrantyMonths, start = l.WarrantyMonths > 0 ? nowStr : null, expiry
                }, tx);

            Db.Exec(c, "UPDATE BatteryUnits SET Status='Sold' WHERE Id=@Id", new { unit.Id }, tx);
            StockRepo.AddMovement(c, tx, unit.Id, "SaleOut", -1, "Sales", saleId, nowStr, null);
        }

        foreach (var e in ex)
        {
            Db.Exec(c, @"INSERT INTO SaleExchanges (SaleId, Description, WeightKg, ExchangeValue)
                         VALUES (@saleId, @Description, @WeightKg, @ExchangeValue)",
                new { saleId, e.Description, e.WeightKg, e.ExchangeValue }, tx);
        }

        if (paidNow > 0)
        {
            var receiptNo = Db.NextNumber(c, tx, "CustomerPayment");
            Db.Exec(c, @"INSERT INTO CustomerPayments (ReceiptNo, CustomerId, SaleId, PaymentDate, Amount, Method, Notes)
                         VALUES (@receiptNo, @customerId, @saleId, @nowStr, @paidNow, @payMethod, 'Paid at sale')",
                new { receiptNo, customerId, saleId, nowStr, paidNow, payMethod = payMethod ?? "Cash" }, tx);
        }

        tx.Commit();
        return saleId;
    }

    public static List<SaleSummary> GetSales(DateTime from, DateTime to, string search = null, long? customerId = null)
    {
        using var c = Db.Open();
        return Db.Query<SaleSummary>(c, @"
            SELECT * FROM vw_SaleSummary
            WHERE SaleDate BETWEEN @f AND @t
              AND (@customerId IS NULL OR CustomerId = @customerId)
              AND (@search IS NULL OR InvoiceNo LIKE @search OR CustomerName LIKE @search OR Phone LIKE @search
                   OR SaleId IN (SELECT si.SaleId FROM SaleItems si JOIN BatteryUnits u ON u.Id = si.BatteryUnitId
                                 WHERE u.SerialNo LIKE @search))
            ORDER BY SaleDate DESC, SaleId DESC",
            new
            {
                f = Db.ToDb(from.Date), t = Db.ToDb(to.Date.AddDays(1).AddSeconds(-1)), customerId,
                search = string.IsNullOrWhiteSpace(search) ? null : "%" + search.Trim() + "%"
            });
    }

    public static SaleReceipt GetReceipt(long saleId)
    {
        using var c = Db.Open();
        var sale = Db.QueryFirst<SaleSummary>(c, "SELECT * FROM vw_SaleSummary WHERE SaleId=@saleId", new { saleId })
                   ?? throw new AppException("Sale not found.");
        var r = new SaleReceipt
        {
            Sale = sale,
            Customer = Db.QueryFirst<Customer>(c, "SELECT * FROM Customers WHERE Id=@CustomerId", sale),
            Items = Db.Query<SaleItemRow>(c, @"
                SELECT b.Name || ' ' || m.ModelName || ' ' || m.CapacityAh || 'Ah' AS Description,
                       u.SerialNo, si.Condition, si.UnitPrice, si.Discount, si.LineTotal, si.WarrantyMonths, si.WarrantyExpiry
                FROM SaleItems si
                JOIN BatteryUnits u  ON u.Id = si.BatteryUnitId
                JOIN BatteryModels m ON m.Id = si.ModelId
                JOIN Brands b        ON b.Id = m.BrandId
                WHERE si.SaleId=@saleId ORDER BY si.Id", new { saleId }),
            Exchanges = Db.Query<SaleExchangeRow>(c, "SELECT Description, WeightKg, ExchangeValue FROM SaleExchanges WHERE SaleId=@saleId", new { saleId }),
            Payments = Db.Query<PaymentRow>(c, "SELECT * FROM CustomerPayments WHERE SaleId=@saleId AND Status='Active'", new { saleId }),
            PrintCount = Db.ScalarLong(c, "SELECT PrintCount FROM Sales WHERE Id=@saleId", new { saleId }),
            CustomerTotalBalance = Db.ScalarLong(c, "SELECT Balance FROM vw_CustomerBalance WHERE CustomerId=@CustomerId", sale)
        };
        return r;
    }

    public static void MarkPrinted(long saleId)
    {
        using var c = Db.Open();
        Db.Exec(c, "UPDATE Sales SET PrintCount = PrintCount + 1 WHERE Id=@saleId", new { saleId });
    }

    /// <summary>Cancels a sale: batteries go back to stock and its payments are voided (refund the cash).</summary>
    public static void VoidSale(long saleId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new AppException("Please give a reason.");
        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        var now = Db.Now();
        var n = Db.Exec(c, "UPDATE Sales SET Status='Voided', VoidReason=@reason, VoidedAt=@now WHERE Id=@saleId AND Status='Active'",
            new { saleId, reason, now }, tx);
        if (n == 0) throw new AppException("Sale not found or already cancelled.");

        var units = Db.Query<IdRow>(c, "SELECT BatteryUnitId AS Id FROM SaleItems WHERE SaleId=@saleId", new { saleId }, tx);
        foreach (var u in units)
        {
            Db.Exec(c, "UPDATE BatteryUnits SET Status='InStock' WHERE Id=@Id AND Status='Sold'", u, tx);
            StockRepo.AddMovement(c, tx, u.Id, "SaleVoidIn", +1, "Sales", saleId, now, "Sale cancelled: " + reason);
        }
        Db.Exec(c, "UPDATE CustomerPayments SET Status='Voided' WHERE SaleId=@saleId", new { saleId }, tx);
        tx.Commit();
    }
}
