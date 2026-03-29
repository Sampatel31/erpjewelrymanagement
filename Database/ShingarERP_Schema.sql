-- ============================================================
-- Shringar Jewellers ERP – Phase 1 SQL Server Schema
-- Modules: 01 (Metal), 02 (Finished Goods), 04 (Stones),
--          15 (Customer KYC), 22 (Accounts & Ledger)
-- Target: SQL Server 2022
-- ============================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ShingarERP')
    CREATE DATABASE ShingarERP COLLATE SQL_Latin1_General_CP1_CI_AS;
GO

USE ShingarERP;
GO

-- ============================================================
-- Shared Master Tables
-- ============================================================

IF OBJECT_ID('Suppliers', 'U') IS NULL
CREATE TABLE Suppliers (
    SupplierId          INT           IDENTITY(1,1) PRIMARY KEY,
    SupplierName        NVARCHAR(100) NOT NULL,
    [Address]           NVARCHAR(200),
    Phone               NVARCHAR(20),
    Email               NVARCHAR(100),
    GSTIN               NVARCHAR(20),
    PAN                 NVARCHAR(10),
    CreditLimit         DECIMAL(18,2) NOT NULL DEFAULT 0,
    OutstandingBalance  DECIMAL(18,2) NOT NULL DEFAULT 0,
    IsActive            BIT           NOT NULL DEFAULT 1,
    CreatedAt           DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_Suppliers_Name   ON Suppliers(SupplierName);
CREATE INDEX IX_Suppliers_GSTIN  ON Suppliers(GSTIN) WHERE GSTIN IS NOT NULL;
GO

-- ============================================================
-- Module 01 – Gold & Metal Inventory
-- ============================================================

IF OBJECT_ID('Metals', 'U') IS NULL
CREATE TABLE Metals (
    MetalId     INT           IDENTITY(1,1) PRIMARY KEY,
    MetalType   NVARCHAR(50)  NOT NULL,
    PurityCode  NVARCHAR(20)  NOT NULL,
    Fineness    DECIMAL(7,3)  NOT NULL,
    WeightUnit  NVARCHAR(20)  NOT NULL DEFAULT 'g',
    IsActive    BIT           NOT NULL DEFAULT 1,
    CreatedAt   DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Metals_TypePurity UNIQUE (MetalType, PurityCode)
);
GO

IF OBJECT_ID('MetalLots', 'U') IS NULL
CREATE TABLE MetalLots (
    LotId               INT           IDENTITY(1,1) PRIMARY KEY,
    LotNumber           NVARCHAR(30)  NOT NULL,
    MetalId             INT           NOT NULL REFERENCES Metals(MetalId),
    SupplierId          INT           NOT NULL REFERENCES Suppliers(SupplierId),
    GrossWeight         DECIMAL(12,4) NOT NULL,
    NetWeight           DECIMAL(12,4) NOT NULL,
    MeltingLossPercent  DECIMAL(7,4)  NOT NULL DEFAULT 0,
    RemainingWeight     DECIMAL(12,4) NOT NULL,
    PurchaseRatePerGram DECIMAL(14,4) NOT NULL,
    TotalCost           DECIMAL(18,4) NOT NULL,
    PurchaseDate        DATE          NOT NULL,
    Remarks             NVARCHAR(500),
    IsActive            BIT           NOT NULL DEFAULT 1,
    CreatedAt           DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_MetalLots_LotNumber UNIQUE (LotNumber)
);
CREATE INDEX IX_MetalLots_Metal     ON MetalLots(MetalId);
CREATE INDEX IX_MetalLots_Supplier  ON MetalLots(SupplierId);
CREATE INDEX IX_MetalLots_Date      ON MetalLots(PurchaseDate);
GO

IF OBJECT_ID('MetalRates', 'U') IS NULL
CREATE TABLE MetalRates (
    RateId      INT           IDENTITY(1,1) PRIMARY KEY,
    MetalId     INT           NOT NULL REFERENCES Metals(MetalId),
    RatePerGram DECIMAL(14,4) NOT NULL,
    RatePerTola DECIMAL(14,4) NOT NULL,
    MCXSpotRate DECIMAL(14,4) NOT NULL,
    RateDate    DATE          NOT NULL,
    [Source]    NVARCHAR(20)  NOT NULL DEFAULT 'Manual',
    CreatedAt   DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_MetalRates_MetalDate ON MetalRates(MetalId, RateDate DESC);
GO

IF OBJECT_ID('MetalPurchases', 'U') IS NULL
CREATE TABLE MetalPurchases (
    PurchaseId    INT           IDENTITY(1,1) PRIMARY KEY,
    VoucherNo     NVARCHAR(30)  NOT NULL,
    MetalId       INT           NOT NULL REFERENCES Metals(MetalId),
    SupplierId    INT           NOT NULL REFERENCES Suppliers(SupplierId),
    LotId         INT           REFERENCES MetalLots(LotId),
    Quantity      DECIMAL(12,4) NOT NULL,
    UnitRate      DECIMAL(14,4) NOT NULL,
    TotalAmount   DECIMAL(18,4) NOT NULL,
    GSTAmount     DECIMAL(18,4) NOT NULL DEFAULT 0,
    NetAmount     DECIMAL(18,4) NOT NULL,
    PurchaseDate  DATE          NOT NULL,
    Remarks       NVARCHAR(500),
    CreatedAt     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_MetalPurchases_VoucherNo UNIQUE (VoucherNo)
);
GO

-- ============================================================
-- Module 02 – Finished Goods Inventory
-- ============================================================

IF OBJECT_ID('ItemCategories', 'U') IS NULL
CREATE TABLE ItemCategories (
    CategoryId    INT           IDENTITY(1,1) PRIMARY KEY,
    CategoryName  NVARCHAR(100) NOT NULL,
    CategoryCode  NVARCHAR(20)  NOT NULL DEFAULT '',
    [Description] NVARCHAR(200),
    IsActive      BIT           NOT NULL DEFAULT 1
);
GO

IF OBJECT_ID('FinishedGoods', 'U') IS NULL
CREATE TABLE FinishedGoods (
    ItemId               INT           IDENTITY(1,1) PRIMARY KEY,
    SKU                  NVARCHAR(30)  NOT NULL,
    ItemName             NVARCHAR(200) NOT NULL,
    CategoryId           INT           NOT NULL REFERENCES ItemCategories(CategoryId),
    MetalId              INT           NOT NULL REFERENCES Metals(MetalId),
    GrossWeight          DECIMAL(7,4)  NOT NULL,
    NetWeight            DECIMAL(7,4)  NOT NULL,
    StoneWeight          DECIMAL(7,4)  NOT NULL DEFAULT 0,
    MakingChargePerGram  DECIMAL(14,2) NOT NULL DEFAULT 0,
    MakingChargePercent  DECIMAL(14,2) NOT NULL DEFAULT 0,
    BarcodeNumber        NVARCHAR(50),
    PhotoPath            NVARCHAR(200),
    RFIDTag              NVARCHAR(50),
    StockLocation        NVARCHAR(50)  NOT NULL DEFAULT 'Showroom',
    StockQuantity        INT           NOT NULL DEFAULT 1,
    SalePrice            DECIMAL(16,2) NOT NULL,
    [Description]        NVARCHAR(500),
    IsActive             BIT           NOT NULL DEFAULT 1,
    CreatedAt            DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt            DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_FinishedGoods_SKU UNIQUE (SKU)
);
CREATE INDEX IX_FinishedGoods_Barcode   ON FinishedGoods(BarcodeNumber) WHERE BarcodeNumber IS NOT NULL;
CREATE INDEX IX_FinishedGoods_Category  ON FinishedGoods(CategoryId);
CREATE INDEX IX_FinishedGoods_Location  ON FinishedGoods(StockLocation);
CREATE INDEX IX_FinishedGoods_Updated   ON FinishedGoods(UpdatedAt);
GO

IF OBJECT_ID('StockTransactions', 'U') IS NULL
CREATE TABLE StockTransactions (
    TransactionId    INT           IDENTITY(1,1) PRIMARY KEY,
    ItemId           INT           NOT NULL REFERENCES FinishedGoods(ItemId),
    VoucherNo        NVARCHAR(30)  NOT NULL,
    TransactionType  NVARCHAR(20)  NOT NULL,
    QuantityIn       INT           NOT NULL DEFAULT 0,
    QuantityOut      INT           NOT NULL DEFAULT 0,
    FromLocation     NVARCHAR(50),
    ToLocation       NVARCHAR(50),
    TransactionDate  DATETIME2     NOT NULL,
    Remarks          NVARCHAR(500),
    CreatedAt        DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_StockTx_Item ON StockTransactions(ItemId, TransactionDate);
GO

-- ============================================================
-- Module 04 – Stone & Diamond Inventory
-- ============================================================

IF OBJECT_ID('Stones', 'U') IS NULL
CREATE TABLE Stones (
    StoneId         INT            IDENTITY(1,1) PRIMARY KEY,
    StoneCode       NVARCHAR(30)   NOT NULL,
    StoneType       NVARCHAR(50)   NOT NULL,
    CertificateNo   NVARCHAR(20),
    CertLab         NVARCHAR(20),
    CaratWeight     DECIMAL(9,4)   NOT NULL,
    Color           NVARCHAR(10),
    Clarity         NVARCHAR(10),
    Cut             NVARCHAR(20),
    Shape           NVARCHAR(20),
    Fluorescence    NVARCHAR(50),
    [Length]        DECIMAL(7,2)   NOT NULL DEFAULT 0,
    Width           DECIMAL(7,2)   NOT NULL DEFAULT 0,
    Depth           DECIMAL(7,2)   NOT NULL DEFAULT 0,
    PurchasePrice   DECIMAL(14,2)  NOT NULL,
    SalePrice       DECIMAL(14,2)  NOT NULL,
    CertificatePath NVARCHAR(200),
    IsConsignment   BIT            NOT NULL DEFAULT 0,
    SupplierId      INT            REFERENCES Suppliers(SupplierId),
    [Status]        NVARCHAR(30)   NOT NULL DEFAULT 'Available',
    IsActive        BIT            NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Stones_Code UNIQUE (StoneCode)
);
CREATE INDEX IX_Stones_CertNo   ON Stones(CertificateNo) WHERE CertificateNo IS NOT NULL;
CREATE INDEX IX_Stones_Status   ON Stones([Status]);
GO

IF OBJECT_ID('FinishedGoodStones', 'U') IS NULL
CREATE TABLE FinishedGoodStones (
    Id               INT           IDENTITY(1,1) PRIMARY KEY,
    ItemId           INT           NOT NULL REFERENCES FinishedGoods(ItemId) ON DELETE CASCADE,
    StoneId          INT           NOT NULL REFERENCES Stones(StoneId),
    Quantity         INT           NOT NULL DEFAULT 1,
    TotalCaratWeight DECIMAL(9,4)  NOT NULL
);
CREATE INDEX IX_FGStones_Item  ON FinishedGoodStones(ItemId);
CREATE INDEX IX_FGStones_Stone ON FinishedGoodStones(StoneId);
GO

-- ============================================================
-- Module 15 – Customer Master & KYC
-- ============================================================

IF OBJECT_ID('Customers', 'U') IS NULL
CREATE TABLE Customers (
    CustomerId           INT           IDENTITY(1,1) PRIMARY KEY,
    FirstName            NVARCHAR(100) NOT NULL,
    LastName             NVARCHAR(100),
    Mobile               NVARCHAR(15)  NOT NULL,
    Email                NVARCHAR(100),
    [Address]            NVARCHAR(500),
    City                 NVARCHAR(100),
    [State]              NVARCHAR(50),
    PinCode              NVARCHAR(10),
    DateOfBirth          DATE,
    AnniversaryDate      DATE,
    Gender               NVARCHAR(20),
    AadhaarNumber        NVARCHAR(20),
    PANNumber            NVARCHAR(10),
    OtherDocType         NVARCHAR(30),
    OtherDocNumber       NVARCHAR(50),
    KYCVerified          BIT           NOT NULL DEFAULT 0,
    KYCVerifiedDate      DATETIME2,
    PhotoPath            NVARCHAR(200),
    LTVScore             DECIMAL(8,2)  NOT NULL DEFAULT 0,
    TotalPurchaseAmount  DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalPurchaseCount   INT           NOT NULL DEFAULT 0,
    LastPurchaseDate     DATETIME2,
    ReferredByCustomerId INT           REFERENCES Customers(CustomerId),
    CustomerCode         NVARCHAR(30),
    IsActive             BIT           NOT NULL DEFAULT 1,
    CreatedAt            DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt            DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_Customers_Mobile   ON Customers(Mobile);
CREATE INDEX IX_Customers_Aadhaar  ON Customers(AadhaarNumber) WHERE AadhaarNumber IS NOT NULL;
CREATE INDEX IX_Customers_PAN      ON Customers(PANNumber) WHERE PANNumber IS NOT NULL;
CREATE UNIQUE INDEX UQ_Customers_Code ON Customers(CustomerCode) WHERE CustomerCode IS NOT NULL;
CREATE INDEX IX_Customers_Name     ON Customers(FirstName, LastName);
CREATE INDEX IX_Customers_LTV      ON Customers(LTVScore DESC);
GO

IF OBJECT_ID('CustomerDocuments', 'U') IS NULL
CREATE TABLE CustomerDocuments (
    DocumentId     INT           IDENTITY(1,1) PRIMARY KEY,
    CustomerId     INT           NOT NULL REFERENCES Customers(CustomerId) ON DELETE CASCADE,
    DocumentType   NVARCHAR(30)  NOT NULL,
    DocumentNumber NVARCHAR(50)  NOT NULL,
    FilePath       NVARCHAR(200),
    IsVerified     BIT           NOT NULL DEFAULT 0,
    VerifiedDate   DATETIME2,
    UploadedAt     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_CustDocs_Customer ON CustomerDocuments(CustomerId);
GO

IF OBJECT_ID('FamilyMembers', 'U') IS NULL
CREATE TABLE FamilyMembers (
    MemberId        INT           IDENTITY(1,1) PRIMARY KEY,
    CustomerId      INT           NOT NULL REFERENCES Customers(CustomerId) ON DELETE CASCADE,
    [Name]          NVARCHAR(100) NOT NULL,
    Relationship    NVARCHAR(30),
    DateOfBirth     DATE,
    AnniversaryDate DATE,
    Mobile          NVARCHAR(15)
);
CREATE INDEX IX_FamilyMembers_Customer ON FamilyMembers(CustomerId);
GO

-- ============================================================
-- Module 22 – Accounts & Ledger (Double-Entry)
-- ============================================================

IF OBJECT_ID('Accounts', 'U') IS NULL
CREATE TABLE Accounts (
    AccountId       INT           IDENTITY(1,1) PRIMARY KEY,
    AccountCode     NVARCHAR(20)  NOT NULL,
    AccountName     NVARCHAR(200) NOT NULL,
    AccountType     NVARCHAR(20)  NOT NULL,
    AccountGroup    NVARCHAR(50),
    ParentAccountId INT           REFERENCES Accounts(AccountId),
    OpeningBalance  DECIMAL(18,4) NOT NULL DEFAULT 0,
    CurrentBalance  DECIMAL(18,4) NOT NULL DEFAULT 0,
    NormalBalance   NVARCHAR(10)  NOT NULL DEFAULT 'Dr',
    IsControl       BIT           NOT NULL DEFAULT 0,
    AllowPosting    BIT           NOT NULL DEFAULT 1,
    IsActive        BIT           NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Accounts_Code UNIQUE (AccountCode)
);
CREATE INDEX IX_Accounts_Type ON Accounts(AccountType);
GO

IF OBJECT_ID('JournalEntries', 'U') IS NULL
CREATE TABLE JournalEntries (
    EntryId          INT           IDENTITY(1,1) PRIMARY KEY,
    VoucherNo        NVARCHAR(30)  NOT NULL,
    VoucherType      NVARCHAR(20)  NOT NULL,
    VoucherDate      DATE          NOT NULL,
    Narration        NVARCHAR(500),
    TotalDebit       DECIMAL(18,4) NOT NULL,
    TotalCredit      DECIMAL(18,4) NOT NULL,
    IsPosted         BIT           NOT NULL DEFAULT 0,
    IsReversed       BIT           NOT NULL DEFAULT 0,
    CreatedByUserId  INT,
    ReferenceNo      NVARCHAR(30),
    CreatedAt        DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt        DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_JournalEntries_VoucherNo UNIQUE (VoucherNo)
);
CREATE INDEX IX_JournalEntries_Date ON JournalEntries(VoucherDate);
CREATE INDEX IX_JournalEntries_Type ON JournalEntries(VoucherType);
GO

IF OBJECT_ID('JournalEntryLines', 'U') IS NULL
CREATE TABLE JournalEntryLines (
    LineId        INT           IDENTITY(1,1) PRIMARY KEY,
    EntryId       INT           NOT NULL REFERENCES JournalEntries(EntryId) ON DELETE CASCADE,
    AccountId     INT           NOT NULL REFERENCES Accounts(AccountId),
    DebitAmount   DECIMAL(18,4) NOT NULL DEFAULT 0,
    CreditAmount  DECIMAL(18,4) NOT NULL DEFAULT 0,
    Narration     NVARCHAR(300),
    SortOrder     INT           NOT NULL DEFAULT 1
);
CREATE INDEX IX_JELines_Entry   ON JournalEntryLines(EntryId);
CREATE INDEX IX_JELines_Account ON JournalEntryLines(AccountId);
GO

IF OBJECT_ID('DayBooks', 'U') IS NULL
CREATE TABLE DayBooks (
    DayBookId      INT           IDENTITY(1,1) PRIMARY KEY,
    BookDate       DATE          NOT NULL,
    BookType       NVARCHAR(20)  NOT NULL DEFAULT 'Cash',
    AccountId      INT           NOT NULL REFERENCES Accounts(AccountId),
    OpeningBalance DECIMAL(18,4) NOT NULL DEFAULT 0,
    TotalReceipts  DECIMAL(18,4) NOT NULL DEFAULT 0,
    TotalPayments  DECIMAL(18,4) NOT NULL DEFAULT 0,
    ClosingBalance DECIMAL(18,4) NOT NULL DEFAULT 0,
    IsClosed       BIT           NOT NULL DEFAULT 0,
    CreatedAt      DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_DayBook_DateTypeAccount UNIQUE (BookDate, BookType, AccountId)
);
GO

-- ============================================================
-- Stored Procedures
-- ============================================================

-- Get metal stock summary
CREATE OR ALTER PROCEDURE sp_GetMetalStockSummary
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        m.MetalId,
        m.MetalType,
        m.PurityCode,
        m.Fineness,
        ISNULL(SUM(l.RemainingWeight), 0) AS TotalRemainingWeight,
        COUNT(l.LotId)                     AS ActiveLotCount,
        MAX(l.PurchaseDate)                AS LastPurchaseDate
    FROM Metals m
    LEFT JOIN MetalLots l ON l.MetalId = m.MetalId AND l.IsActive = 1 AND l.RemainingWeight > 0
    WHERE m.IsActive = 1
    GROUP BY m.MetalId, m.MetalType, m.PurityCode, m.Fineness
    ORDER BY m.MetalType, m.Fineness DESC;
END;
GO

-- Get trial balance for a period
CREATE OR ALTER PROCEDURE sp_GetTrialBalance
    @AsOfDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        a.AccountCode,
        a.AccountName,
        a.AccountType,
        a.NormalBalance,
        a.OpeningBalance,
        ISNULL(SUM(l.DebitAmount),  0) AS TotalDebit,
        ISNULL(SUM(l.CreditAmount), 0) AS TotalCredit,
        a.OpeningBalance
          + ISNULL(SUM(l.DebitAmount),  0)
          - ISNULL(SUM(l.CreditAmount), 0) AS NetBalance
    FROM Accounts a
    LEFT JOIN JournalEntryLines l ON l.AccountId = a.AccountId
    LEFT JOIN JournalEntries    e ON e.EntryId   = l.EntryId
                                  AND e.IsPosted = 1
                                  AND e.IsReversed = 0
                                  AND e.VoucherDate <= @AsOfDate
    WHERE a.IsActive = 1 AND a.AllowPosting = 1
    GROUP BY a.AccountCode, a.AccountName, a.AccountType, a.NormalBalance, a.OpeningBalance
    ORDER BY a.AccountCode;
END;
GO

-- Customer LTV scoring
CREATE OR ALTER PROCEDURE sp_RecalculateCustomerLTV
    @CustomerId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @score DECIMAL(18,2) = 0;
    DECLARE @amount DECIMAL(18,2), @count INT, @lastDate DATETIME2;

    SELECT @amount = TotalPurchaseAmount,
           @count  = TotalPurchaseCount,
           @lastDate = LastPurchaseDate
    FROM Customers WHERE CustomerId = @CustomerId;

    -- Monetary (capped at 500)
    SET @score = @score + CASE WHEN @amount / 10000.0 > 500 THEN 500 ELSE @amount / 10000.0 END;
    -- Frequency (capped at 300)
    SET @score = @score + CASE WHEN @count * 5.0 > 300 THEN 300 ELSE @count * 5.0 END;
    -- Recency (capped at 200)
    IF @lastDate IS NOT NULL
    BEGIN
        DECLARE @daysSince INT = DATEDIFF(day, @lastDate, SYSUTCDATETIME());
        SET @score = @score + CASE WHEN 200.0 - (@daysSince / 365.0 * 200.0) < 0 THEN 0
                                   ELSE 200.0 - (@daysSince / 365.0 * 200.0)  END;
    END

    SET @score = CASE WHEN @score > 1000 THEN 1000 ELSE @score END;

    UPDATE Customers SET LTVScore = @score, UpdatedAt = SYSUTCDATETIME()
    WHERE CustomerId = @CustomerId;

    SELECT @score AS NewLTVScore;
END;
GO

PRINT 'Shringar ERP Phase 1 schema created successfully.';
GO
