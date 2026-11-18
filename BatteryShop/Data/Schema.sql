-- =====================================================================
--  Battery Shop Management System  -  Phase 1 Database Schema (SQLite)
-- ---------------------------------------------------------------------
--  Conventions
--    * Money  : INTEGER, whole rupees (Rs. 18,500 -> 18500). No floats.
--    * Dates  : TEXT, local time, ISO format 'YYYY-MM-DD HH:MM:SS'
--    * Bools  : INTEGER 0/1
--    * Nothing is hard-deleted: records are deactivated or voided.
--    * Balances and stock are CALCULATED (see views at the bottom),
--      never stored, so they can't drift out of sync.
-- =====================================================================

-- (PRAGMAs foreign_keys / journal_mode are set by the app on every connection)

-- ---------------------------------------------------------------------
-- 1. SETUP
-- ---------------------------------------------------------------------

-- Single-row table with shop details printed on receipts
CREATE TABLE ShopSettings (
    Id                  INTEGER PRIMARY KEY CHECK (Id = 1),
    ShopName            TEXT    NOT NULL,
    Address             TEXT,
    Phone               TEXT,
    LogoPath            TEXT,
    ReceiptFooter       TEXT,              -- warranty terms / thank-you note
    ReceiptPrinterName  TEXT,
    BackupFolder        TEXT,
    LastBackupAt        TEXT
);

-- Kept minimal now; roles are used in Phase 3
CREATE TABLE Users (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    FullName      TEXT    NOT NULL,
    Username      TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    PinHash       TEXT    NOT NULL,
    Role          TEXT    NOT NULL DEFAULT 'Owner'
                          CHECK (Role IN ('Owner','Salesman')),
    IsActive      INTEGER NOT NULL DEFAULT 1,
    CreatedAt     TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);

-- Running numbers for invoices: SAL-000123, PUR-000045, RCV-000010
CREATE TABLE NumberSequences (
    Name        TEXT    PRIMARY KEY,          -- 'Sale','Purchase','CustomerPayment','SupplierPayment'
    Prefix      TEXT    NOT NULL,
    NextValue   INTEGER NOT NULL DEFAULT 1
);

-- ---------------------------------------------------------------------
-- 2. PRODUCT CATALOG
-- ---------------------------------------------------------------------

CREATE TABLE Brands (
    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
    Name       TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    IsActive   INTEGER NOT NULL DEFAULT 1
);

-- One row per battery model, e.g. "Brand X  NS70  65Ah"
CREATE TABLE BatteryModels (
    Id                   INTEGER PRIMARY KEY AUTOINCREMENT,
    BrandId              INTEGER NOT NULL REFERENCES Brands(Id),
    ModelName            TEXT    NOT NULL,
    CapacityAh           INTEGER NOT NULL,
    Voltage              INTEGER NOT NULL DEFAULT 12,
    BatteryType          TEXT    NOT NULL DEFAULT 'Lead Acid'
                                 CHECK (BatteryType IN ('Lead Acid','Dry/MF','Tubular','Lithium','Other')),
    ApproxWeightKg       REAL,                       -- helps estimate scrap weight later
    DefaultSalePrice     INTEGER NOT NULL DEFAULT 0, -- suggested price, editable at sale
    WarrantyMonths       INTEGER NOT NULL DEFAULT 0,
    LowStockLevel        INTEGER NOT NULL DEFAULT 2,
    IsActive             INTEGER NOT NULL DEFAULT 1,
    CreatedAt            TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    UNIQUE (BrandId, ModelName, CapacityAh)
);

-- ---------------------------------------------------------------------
-- 3. SUPPLIERS & PURCHASES
-- ---------------------------------------------------------------------

CREATE TABLE Suppliers (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    Name             TEXT    NOT NULL,
    Phone            TEXT,
    Address          TEXT,
    OpeningBalance   INTEGER NOT NULL DEFAULT 0,  -- amount YOU owe them at start
    IsActive         INTEGER NOT NULL DEFAULT 1,
    CreatedAt        TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);

CREATE TABLE Purchases (
    Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    PurchaseNo         TEXT    NOT NULL UNIQUE,       -- our number
    SupplierId         INTEGER NOT NULL REFERENCES Suppliers(Id),
    SupplierInvoiceNo  TEXT,                          -- dealer's bill number
    PurchaseDate       TEXT    NOT NULL,
    SubTotal           INTEGER NOT NULL DEFAULT 0,
    Discount           INTEGER NOT NULL DEFAULT 0,
    NetTotal           INTEGER NOT NULL DEFAULT 0,    -- SubTotal - Discount
    Notes              TEXT,
    Status             TEXT    NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active','Voided')),
    VoidReason         TEXT,
    CreatedBy          INTEGER REFERENCES Users(Id),
    CreatedAt          TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);

CREATE TABLE PurchaseItems (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    PurchaseId     INTEGER NOT NULL REFERENCES Purchases(Id),
    ModelId        INTEGER NOT NULL REFERENCES BatteryModels(Id),
    Quantity       INTEGER NOT NULL CHECK (Quantity > 0),
    UnitCost       INTEGER NOT NULL CHECK (UnitCost >= 0),
    LineTotal      INTEGER NOT NULL                -- Quantity * UnitCost
);

CREATE TABLE SupplierPayments (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    PaymentNo     TEXT    NOT NULL UNIQUE,
    SupplierId    INTEGER NOT NULL REFERENCES Suppliers(Id),
    PurchaseId    INTEGER REFERENCES Purchases(Id),  -- optional link
    PaymentDate   TEXT    NOT NULL,
    Amount        INTEGER NOT NULL CHECK (Amount > 0),
    Method        TEXT    NOT NULL DEFAULT 'Cash'
                          CHECK (Method IN ('Cash','Bank','Cheque','Mobile Wallet')),
    Notes         TEXT,
    Status        TEXT    NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active','Voided')),
    CreatedBy     INTEGER REFERENCES Users(Id),
    CreatedAt     TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);

-- ---------------------------------------------------------------------
-- 4. STOCK  (every physical battery is one row)
-- ---------------------------------------------------------------------

-- Each battery in the shop. Stock count = rows with Status 'InStock'.
-- New batteries come from PurchaseItems; second-hand ones can be
-- added manually now and from exchanges in Phase 2.
CREATE TABLE BatteryUnits (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    ModelId          INTEGER NOT NULL REFERENCES BatteryModels(Id),
    SerialNo         TEXT    COLLATE NOCASE,     -- optional but strongly recommended
    DateCode         TEXT,                       -- manufacturer date code
    Condition        TEXT    NOT NULL DEFAULT 'New'
                             CHECK (Condition IN ('New','SecondHand')),
    ConditionNote    TEXT,                       -- e.g. "80% backup, tested OK"
    Source           TEXT    NOT NULL DEFAULT 'Purchase'
                             CHECK (Source IN ('Purchase','Exchange','Opening','Manual')),
    PurchaseItemId   INTEGER REFERENCES PurchaseItems(Id),
    UnitCost         INTEGER NOT NULL DEFAULT 0, -- actual cost of THIS battery
    SalePrice        INTEGER,                    -- set for second-hand units; NULL = use model default
    Status           TEXT    NOT NULL DEFAULT 'InStock'
                             CHECK (Status IN ('InStock','Sold','Returned','Claimed','Scrapped','Removed')),
    ReceivedDate     TEXT    NOT NULL,
    CreatedAt        TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);
-- A serial number can't be used twice (NULLs are allowed)
CREATE UNIQUE INDEX UX_BatteryUnits_Serial ON BatteryUnits(SerialNo) WHERE SerialNo IS NOT NULL;
CREATE INDEX IX_BatteryUnits_ModelStatus ON BatteryUnits(ModelId, Status);

-- History of every stock in/out, for audit and the stock register report
CREATE TABLE StockMovements (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    BatteryUnitId   INTEGER NOT NULL REFERENCES BatteryUnits(Id),
    MovementType    TEXT    NOT NULL
                    CHECK (MovementType IN ('PurchaseIn','OpeningIn','ExchangeIn','SaleOut',
                                            'SaleVoidIn','ClaimOut','ClaimIn','AdjustIn','AdjustOut')),
    Direction       INTEGER NOT NULL CHECK (Direction IN (1,-1)),   -- +1 in, -1 out
    RefTable        TEXT,           -- 'Purchases','Sales', ...
    RefId           INTEGER,
    MovementDate    TEXT    NOT NULL,
    Notes           TEXT,
    CreatedBy       INTEGER REFERENCES Users(Id),
    CreatedAt       TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IX_StockMovements_Date ON StockMovements(MovementDate);

-- ---------------------------------------------------------------------
-- 5. CUSTOMERS
-- ---------------------------------------------------------------------

CREATE TABLE Customers (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    Name             TEXT    NOT NULL,
    Phone            TEXT,
    Address          TEXT,
    VehicleInfo      TEXT,                      -- optional: vehicle / UPS / solar
    OpeningBalance   INTEGER NOT NULL DEFAULT 0, -- amount customer owed you at start
    IsWalkIn         INTEGER NOT NULL DEFAULT 0, -- one shared "Walk-in Customer" row
    IsActive         INTEGER NOT NULL DEFAULT 1,
    CreatedAt        TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE UNIQUE INDEX UX_Customers_Phone ON Customers(Phone) WHERE Phone IS NOT NULL AND Phone <> '';

-- ---------------------------------------------------------------------
-- 6. SALES
-- ---------------------------------------------------------------------

CREATE TABLE Sales (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    InvoiceNo        TEXT    NOT NULL UNIQUE,
    CustomerId       INTEGER NOT NULL REFERENCES Customers(Id),
    SaleDate         TEXT    NOT NULL,
    SubTotal         INTEGER NOT NULL DEFAULT 0,  -- sum of SaleItems.LineTotal
    Discount         INTEGER NOT NULL DEFAULT 0,  -- bill-level discount
    ExchangeTotal    INTEGER NOT NULL DEFAULT 0,  -- sum of SaleExchanges.ExchangeValue
    NetTotal         INTEGER NOT NULL DEFAULT 0,  -- SubTotal - Discount - ExchangeTotal
    PaymentType      TEXT    NOT NULL DEFAULT 'Cash'
                             CHECK (PaymentType IN ('Cash','Partial','Credit')),
    Notes            TEXT,
    Status           TEXT    NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active','Voided')),
    VoidReason       TEXT,
    VoidedAt         TEXT,
    PrintCount       INTEGER NOT NULL DEFAULT 0,  -- for "Reprint" / "Duplicate copy"
    CreatedBy        INTEGER REFERENCES Users(Id),
    CreatedAt        TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IX_Sales_Date     ON Sales(SaleDate);
CREATE INDEX IX_Sales_Customer ON Sales(CustomerId);

-- One row per battery sold (always a specific unit)
CREATE TABLE SaleItems (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    SaleId           INTEGER NOT NULL REFERENCES Sales(Id),
    BatteryUnitId    INTEGER NOT NULL REFERENCES BatteryUnits(Id),
    ModelId          INTEGER NOT NULL REFERENCES BatteryModels(Id),
    Condition        TEXT    NOT NULL CHECK (Condition IN ('New','SecondHand')),
    UnitPrice        INTEGER NOT NULL,
    Discount         INTEGER NOT NULL DEFAULT 0,
    LineTotal        INTEGER NOT NULL,            -- UnitPrice - Discount
    CostAtSale       INTEGER NOT NULL,            -- copied from BatteryUnits.UnitCost (for profit)
    WarrantyMonths   INTEGER NOT NULL DEFAULT 0,
    WarrantyStart    TEXT,
    WarrantyExpiry   TEXT
);
CREATE INDEX IX_SaleItems_Sale ON SaleItems(SaleId);
CREATE INDEX IX_SaleItems_Unit ON SaleItems(BatteryUnitId);

-- Old batteries taken from the customer during a sale.
-- Phase 1 records them; Phase 2 moves them into scrap or second-hand stock.
CREATE TABLE SaleExchanges (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    SaleId           INTEGER NOT NULL REFERENCES Sales(Id),
    Description      TEXT,                        -- e.g. "Old 65Ah, any brand"
    BrandId          INTEGER REFERENCES Brands(Id),
    CapacityAh       INTEGER,
    WeightKg         REAL,
    ExchangeValue    INTEGER NOT NULL CHECK (ExchangeValue >= 0),
    Disposition      TEXT    NOT NULL DEFAULT 'Pending'
                             CHECK (Disposition IN ('Pending','Scrap','SecondHand')),
    BatteryUnitId    INTEGER REFERENCES BatteryUnits(Id)  -- filled if moved to second-hand stock
);

-- Every rupee received from a customer: at sale time OR later.
-- "Paid" on a receipt = SUM(Amount) WHERE SaleId = that sale.
CREATE TABLE CustomerPayments (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    ReceiptNo     TEXT    NOT NULL UNIQUE,
    CustomerId    INTEGER NOT NULL REFERENCES Customers(Id),
    SaleId        INTEGER REFERENCES Sales(Id),     -- NULL = general payment against balance
    PaymentDate   TEXT    NOT NULL,
    Amount        INTEGER NOT NULL CHECK (Amount > 0),
    Method        TEXT    NOT NULL DEFAULT 'Cash'
                          CHECK (Method IN ('Cash','Bank','Cheque','Mobile Wallet')),
    Notes         TEXT,
    Status        TEXT    NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active','Voided')),
    CreatedBy     INTEGER REFERENCES Users(Id),
    CreatedAt     TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IX_CustomerPayments_Customer ON CustomerPayments(CustomerId);
CREATE INDEX IX_CustomerPayments_Sale     ON CustomerPayments(SaleId);

-- =====================================================================
-- 7. VIEWS  (calculated values: stock, balances, receipts)
-- =====================================================================

-- Current stock per model, split by condition
CREATE VIEW vw_ModelStock AS
SELECT  m.Id                AS ModelId,
        b.Name              AS Brand,
        m.ModelName,
        m.CapacityAh,
        m.LowStockLevel,
        SUM(CASE WHEN u.Status='InStock' AND u.Condition='New'        THEN 1 ELSE 0 END) AS NewInStock,
        SUM(CASE WHEN u.Status='InStock' AND u.Condition='SecondHand' THEN 1 ELSE 0 END) AS SecondHandInStock,
        SUM(CASE WHEN u.Status='InStock' THEN u.UnitCost ELSE 0 END)                     AS StockValueAtCost
FROM    BatteryModels m
JOIN    Brands b            ON b.Id = m.BrandId
LEFT JOIN BatteryUnits u    ON u.ModelId = m.Id
WHERE   m.IsActive = 1
GROUP BY m.Id;

-- Each sale with paid and remaining amounts (used for receipts and history)
CREATE VIEW vw_SaleSummary AS
SELECT  s.Id AS SaleId, s.InvoiceNo, s.SaleDate, s.CustomerId, c.Name AS CustomerName, c.Phone,
        s.SubTotal, s.Discount, s.ExchangeTotal, s.NetTotal, s.PaymentType, s.Status,
        COALESCE((SELECT SUM(p.Amount) FROM CustomerPayments p
                  WHERE p.SaleId = s.Id AND p.Status='Active'), 0)                 AS PaidAmount,
        s.NetTotal - COALESCE((SELECT SUM(p.Amount) FROM CustomerPayments p
                  WHERE p.SaleId = s.Id AND p.Status='Active'), 0)                 AS RemainingAmount,
        (SELECT SUM(i.LineTotal - i.CostAtSale) FROM SaleItems i WHERE i.SaleId = s.Id)
            - s.Discount                                                            AS GrossProfit
FROM    Sales s
JOIN    Customers c ON c.Id = s.CustomerId;

-- Customer outstanding balance = opening + sales - payments
CREATE VIEW vw_CustomerBalance AS
SELECT  c.Id AS CustomerId, c.Name, c.Phone,
        c.OpeningBalance,
        COALESCE((SELECT SUM(NetTotal) FROM Sales s
                  WHERE s.CustomerId = c.Id AND s.Status='Active'), 0)            AS TotalSales,
        COALESCE((SELECT SUM(Amount) FROM CustomerPayments p
                  WHERE p.CustomerId = c.Id AND p.Status='Active'), 0)            AS TotalPaid,
        c.OpeningBalance
        + COALESCE((SELECT SUM(NetTotal) FROM Sales s
                    WHERE s.CustomerId = c.Id AND s.Status='Active'), 0)
        - COALESCE((SELECT SUM(Amount) FROM CustomerPayments p
                    WHERE p.CustomerId = c.Id AND p.Status='Active'), 0)          AS Balance
FROM    Customers c;

-- Customer account statement (one line per sale or payment; running
-- balance is calculated in the app or with a window function)
CREATE VIEW vw_CustomerLedger AS
SELECT  CustomerId, SaleDate AS TxnDate, 'Sale' AS TxnType, InvoiceNo AS RefNo,
        NetTotal AS Debit, 0 AS Credit, Id AS RefId
FROM    Sales WHERE Status = 'Active'
UNION ALL
SELECT  CustomerId, PaymentDate, 'Payment', ReceiptNo,
        0, Amount, Id
FROM    CustomerPayments WHERE Status = 'Active';

-- Supplier balance = opening + purchases - payments  (what you owe)
CREATE VIEW vw_SupplierBalance AS
SELECT  sp.Id AS SupplierId, sp.Name, sp.Phone,
        sp.OpeningBalance
        + COALESCE((SELECT SUM(NetTotal) FROM Purchases p
                    WHERE p.SupplierId = sp.Id AND p.Status='Active'), 0)
        - COALESCE((SELECT SUM(Amount) FROM SupplierPayments x
                    WHERE x.SupplierId = sp.Id AND x.Status='Active'), 0)        AS Balance
FROM    Suppliers sp;

-- =====================================================================
-- 8. SEED DATA
-- =====================================================================

INSERT INTO ShopSettings (Id, ShopName, Address, Phone, ReceiptFooter)
VALUES (1, 'Ayaz Battery & Solar Service',
        'Rahman Market, Abbasi Chowk, Lari Adda, Chanar Rd, Mansehra, 21300',
        '+92 303 4093399',
        'Warranty valid with this receipt only. Physical damage, water/fire damage and overcharging are not covered.');

INSERT INTO NumberSequences (Name, Prefix) VALUES
    ('Sale',            'SAL-'),
    ('Purchase',        'PUR-'),
    ('CustomerPayment', 'RCV-'),
    ('SupplierPayment', 'PAY-');

INSERT INTO Customers (Name, IsWalkIn) VALUES ('Walk-in Customer', 1);

-- Default login: username 'admin', PIN '1234' (SHA-256). Change it in Settings.
INSERT INTO Users (FullName, Username, PinHash, Role)
VALUES ('Owner', 'admin', '03ac674216f3e15c761ee1a5e255f067953623c8b388b4459e13f978d7c846f4', 'Owner');
