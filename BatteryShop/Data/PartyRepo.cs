using BatteryShop.Models;

namespace BatteryShop.Data;

/// <summary>Customers, suppliers, their payments and account statements.</summary>
public static class PartyRepo
{
    // ================================================================ Customers

    public static List<Customer> GetCustomers(string search = null, bool onlyWithBalance = false)
    {
        using var c = Db.Open();
        return Db.Query<Customer>(c, $@"
            SELECT cu.*, v.Balance
            FROM Customers cu JOIN vw_CustomerBalance v ON v.CustomerId = cu.Id
            WHERE cu.IsActive = 1
              AND (@search IS NULL OR cu.Name LIKE @search OR cu.Phone LIKE @search)
              {(onlyWithBalance ? "AND v.Balance <> 0" : "")}
            ORDER BY cu.IsWalkIn DESC, cu.Name",
            new { search = string.IsNullOrWhiteSpace(search) ? null : "%" + search.Trim() + "%" });
    }

    public static Customer GetCustomer(long id)
    {
        using var c = Db.Open();
        return Db.QueryFirst<Customer>(c, @"SELECT cu.*, v.Balance FROM Customers cu
                                            JOIN vw_CustomerBalance v ON v.CustomerId = cu.Id WHERE cu.Id=@id", new { id });
    }

    public static Customer GetWalkInCustomer()
    {
        using var c = Db.Open();
        return Db.QueryFirst<Customer>(c, "SELECT *, 0 AS Balance FROM Customers WHERE IsWalkIn = 1 ORDER BY Id LIMIT 1");
    }

    public static long SaveCustomer(Customer cu)
    {
        if (string.IsNullOrWhiteSpace(cu.Name)) throw new AppException("Customer name is required.");
        var phone = string.IsNullOrWhiteSpace(cu.Phone) ? null : cu.Phone.Trim();
        CheckPhone(phone);
        using var c = Db.Open();
        if (phone != null && Db.ScalarLong(c, "SELECT COUNT(*) FROM Customers WHERE Phone=@phone AND Id<>@Id", new { phone, cu.Id }) > 0)
            throw new AppException($"Another customer already has phone number {phone}.");

        var args = new { cu.Id, Name = cu.Name.Trim(), phone, cu.Address, cu.VehicleInfo, cu.OpeningBalance, cu.IsActive };
        if (cu.Id == 0)
            return cu.Id = Db.Insert(c, @"INSERT INTO Customers (Name, Phone, Address, VehicleInfo, OpeningBalance, IsActive)
                                          VALUES (@Name, @phone, @Address, @VehicleInfo, @OpeningBalance, @IsActive)", args);
        Db.Exec(c, @"UPDATE Customers SET Name=@Name, Phone=@phone, Address=@Address, VehicleInfo=@VehicleInfo,
                     OpeningBalance=@OpeningBalance, IsActive=@IsActive WHERE Id=@Id", args);
        return cu.Id;
    }

    /// <summary>Records money received from a customer against their balance (not tied to one bill).</summary>
    public static PaymentRow ReceivePayment(long customerId, long amount, string method, string notes, DateTime? date = null)
    {
        if (amount <= 0) throw new AppException("Amount must be greater than 0.");
        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        var no = Db.NextNumber(c, tx, "CustomerPayment");
        var id = Db.Insert(c, @"INSERT INTO CustomerPayments (ReceiptNo, CustomerId, SaleId, PaymentDate, Amount, Method, Notes)
                                VALUES (@no, @customerId, NULL, @date, @amount, @method, @notes)",
            new { no, customerId, date = Db.ToDb(date ?? DateTime.Now), amount, method, notes }, tx);
        tx.Commit();
        return GetPayment(id);
    }

    public static PaymentRow GetPayment(long id)
    {
        using var c = Db.Open();
        return Db.QueryFirst<PaymentRow>(c, @"
            SELECT p.*, cu.Name AS CustomerName, s.InvoiceNo
            FROM CustomerPayments p JOIN Customers cu ON cu.Id = p.CustomerId
            LEFT JOIN Sales s ON s.Id = p.SaleId WHERE p.Id = @id", new { id });
    }

    public static List<PaymentRow> GetPayments(DateTime from, DateTime to, long? customerId = null)
    {
        using var c = Db.Open();
        return Db.Query<PaymentRow>(c, @"
            SELECT p.*, cu.Name AS CustomerName, s.InvoiceNo
            FROM CustomerPayments p JOIN Customers cu ON cu.Id = p.CustomerId
            LEFT JOIN Sales s ON s.Id = p.SaleId
            WHERE p.PaymentDate BETWEEN @f AND @t AND (@customerId IS NULL OR p.CustomerId = @customerId)
            ORDER BY p.PaymentDate DESC, p.Id DESC",
            new { f = Db.ToDb(from.Date), t = Db.ToDb(to.Date.AddDays(1).AddSeconds(-1)), customerId });
    }

    public static void VoidPayment(long paymentId, string reason)
    {
        using var c = Db.Open();
        var n = Db.Exec(c, @"UPDATE CustomerPayments SET Status='Voided', Notes = COALESCE(Notes || ' | ', '') || 'VOID: ' || @reason
                             WHERE Id=@paymentId AND Status='Active'", new { paymentId, reason });
        if (n == 0) throw new AppException("Payment not found or already voided.");
    }

    /// <summary>Customer account statement with running balance. Opening balance is the first row.</summary>
    public static List<LedgerRow> GetCustomerLedger(long customerId, DateTime? from = null, DateTime? to = null)
    {
        using var c = Db.Open();
        var opening = Db.ScalarLong(c, "SELECT OpeningBalance FROM Customers WHERE Id=@customerId", new { customerId });
        var rows = Db.Query<LedgerRow>(c, @"
            SELECT TxnDate, TxnType, RefNo, Debit, Credit FROM vw_CustomerLedger
            WHERE CustomerId = @customerId ORDER BY TxnDate, TxnType DESC, RefId", new { customerId });
        return BuildLedger(opening, rows, from, to);
    }

    // ================================================================ Suppliers

    public static List<Supplier> GetSuppliers(bool activeOnly = true)
    {
        using var c = Db.Open();
        return Db.Query<Supplier>(c, $@"
            SELECT s.*, v.Balance FROM Suppliers s JOIN vw_SupplierBalance v ON v.SupplierId = s.Id
            {(activeOnly ? "WHERE s.IsActive = 1" : "")} ORDER BY s.Name");
    }

    public static long SaveSupplier(Supplier s)
    {
        if (string.IsNullOrWhiteSpace(s.Name)) throw new AppException("Supplier name is required.");
        CheckPhone(string.IsNullOrWhiteSpace(s.Phone) ? null : s.Phone.Trim());
        using var c = Db.Open();
        var args = new { s.Id, Name = s.Name.Trim(), s.Phone, s.Address, s.OpeningBalance, s.IsActive };
        if (s.Id == 0)
            return s.Id = Db.Insert(c, @"INSERT INTO Suppliers (Name, Phone, Address, OpeningBalance, IsActive)
                                         VALUES (@Name, @Phone, @Address, @OpeningBalance, @IsActive)", args);
        Db.Exec(c, @"UPDATE Suppliers SET Name=@Name, Phone=@Phone, Address=@Address, OpeningBalance=@OpeningBalance,
                     IsActive=@IsActive WHERE Id=@Id", args);
        return s.Id;
    }

    public static string PaySupplier(long supplierId, long amount, string method, string notes, long? purchaseId = null, DateTime? date = null)
    {
        if (amount <= 0) throw new AppException("Amount must be greater than 0.");
        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        var no = PaySupplier(c, tx, supplierId, amount, method, notes, purchaseId, Db.ToDb(date ?? DateTime.Now));
        tx.Commit();
        return no;
    }

    internal static string PaySupplier(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction tx,
        long supplierId, long amount, string method, string notes, long? purchaseId, string date)
    {
        var no = Db.NextNumber(c, tx, "SupplierPayment");
        Db.Exec(c, @"INSERT INTO SupplierPayments (PaymentNo, SupplierId, PurchaseId, PaymentDate, Amount, Method, Notes)
                     VALUES (@no, @supplierId, @purchaseId, @date, @amount, @method, @notes)",
            new { no, supplierId, purchaseId, date, amount, method, notes }, tx);
        return no;
    }

    /// <summary>Supplier statement: Debit = purchase (you owe more), Credit = payment.</summary>
    public static List<LedgerRow> GetSupplierLedger(long supplierId, DateTime? from = null, DateTime? to = null)
    {
        using var c = Db.Open();
        var opening = Db.ScalarLong(c, "SELECT OpeningBalance FROM Suppliers WHERE Id=@supplierId", new { supplierId });
        var rows = Db.Query<LedgerRow>(c, @"
            SELECT PurchaseDate AS TxnDate, 'Purchase' AS TxnType,
                   PurchaseNo || COALESCE(' / ' || SupplierInvoiceNo, '') AS RefNo, NetTotal AS Debit, 0 AS Credit
            FROM Purchases WHERE SupplierId=@supplierId AND Status='Active'
            UNION ALL
            SELECT PaymentDate, 'Payment', PaymentNo, 0, Amount
            FROM SupplierPayments WHERE SupplierId=@supplierId AND Status='Active'
            ORDER BY 1, 2 DESC", new { supplierId });
        return BuildLedger(opening, rows, from, to);
    }

    // ================================================================ Shared

    /// <summary>Phone is optional; if given it must have 10 to 13 digits (e.g. 03001234567 or +92 300 1234567).</summary>
    private static void CheckPhone(string phone)
    {
        if (phone == null) return;
        if (phone.Any(ch => !(char.IsAsciiDigit(ch) || ch == '+' || ch == '-' || ch == ' ')))
            throw new AppException("Phone number can only contain digits, +, - and spaces.");
        var digits = phone.Count(char.IsAsciiDigit);
        if (digits < 10 || digits > 13) throw new AppException($"Phone number '{phone}' does not look complete (10 to 13 digits expected).");
    }

    private static List<LedgerRow> BuildLedger(long opening, List<LedgerRow> rows, DateTime? from, DateTime? to)
    {
        var f = from.HasValue ? Db.ToDb(from.Value.Date) : null;
        var t = to.HasValue ? Db.ToDb(to.Value.Date.AddDays(1).AddSeconds(-1)) : null;

        long balance = opening;
        // Everything before 'from' is folded into the opening balance line
        foreach (var r in rows.Where(r => f != null && string.CompareOrdinal(r.TxnDate, f) < 0))
            balance += r.Debit - r.Credit;

        var result = new List<LedgerRow> { new() { TxnDate = f ?? "", TxnType = "Opening Balance", Balance = balance } };
        foreach (var r in rows.Where(r => (f == null || string.CompareOrdinal(r.TxnDate, f) >= 0)
                                       && (t == null || string.CompareOrdinal(r.TxnDate, t) <= 0)))
        {
            balance += r.Debit - r.Credit;
            r.Balance = balance;
            result.Add(r);
        }
        return result;
    }
}
