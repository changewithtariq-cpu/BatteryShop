using BatteryShop.Models;

namespace BatteryShop.Data;

public static class ReportRepo
{
    public static DashboardData GetDashboard()
    {
        using var c = Db.Open();
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var dayArgs = new { f = Db.ToDb(today), t = Db.ToDb(today.AddDays(1).AddSeconds(-1)) };
        var monthArgs = new { f = Db.ToDb(monthStart), t = Db.ToDb(today.AddDays(1).AddSeconds(-1)) };

        const string salesSql = "SELECT COALESCE(SUM(NetTotal),0) FROM Sales WHERE Status='Active' AND SaleDate BETWEEN @f AND @t";
        const string profitSql = "SELECT COALESCE(SUM(GrossProfit),0) FROM vw_SaleSummary WHERE Status='Active' AND SaleDate BETWEEN @f AND @t";

        var d = new DashboardData
        {
            TodaySales = Db.ScalarLong(c, salesSql, dayArgs),
            TodayBills = Db.ScalarLong(c, "SELECT COUNT(*) FROM Sales WHERE Status='Active' AND SaleDate BETWEEN @f AND @t", dayArgs),
            TodayCollection = Db.ScalarLong(c, "SELECT COALESCE(SUM(Amount),0) FROM CustomerPayments WHERE Status='Active' AND PaymentDate BETWEEN @f AND @t", dayArgs),
            TodayProfit = Db.ScalarLong(c, profitSql, dayArgs),
            MonthSales = Db.ScalarLong(c, salesSql, monthArgs),
            MonthProfit = Db.ScalarLong(c, profitSql, monthArgs),
            CustomerReceivable = Db.ScalarLong(c, "SELECT COALESCE(SUM(Balance),0) FROM vw_CustomerBalance WHERE Balance > 0"),
            SupplierPayable = Db.ScalarLong(c, "SELECT COALESCE(SUM(Balance),0) FROM vw_SupplierBalance WHERE Balance > 0"),
            NewStockQty = Db.ScalarLong(c, "SELECT COUNT(*) FROM BatteryUnits WHERE Status='InStock' AND Condition='New'"),
            SecondHandStockQty = Db.ScalarLong(c, "SELECT COUNT(*) FROM BatteryUnits WHERE Status='InStock' AND Condition='SecondHand'"),
            StockValue = Db.ScalarLong(c, "SELECT COALESCE(SUM(UnitCost),0) FROM BatteryUnits WHERE Status='InStock'"),
            PendingExchanges = Db.ScalarLong(c, @"SELECT COUNT(*) FROM SaleExchanges e JOIN Sales s ON s.Id = e.SaleId
                                                  WHERE e.Disposition='Pending' AND s.Status='Active'"),
            LowStock = Db.Query<ModelStock>(c, "SELECT * FROM vw_ModelStock WHERE NewInStock <= LowStockLevel ORDER BY NewInStock, Brand")
        };
        return d;
    }
}
