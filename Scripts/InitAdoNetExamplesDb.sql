/* =========================
   Create DB from scratch
   ========================= */
IF DB_ID(N'AdoNetExamples') IS NULL
BEGIN
    CREATE DATABASE [AdoNetExamples];
END
GO

USE [AdoNetExamples];
GO

/* =========================
   Core Tables
   ========================= */

/* Customers */
IF OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL DROP TABLE dbo.Customers;
GO
CREATE TABLE dbo.Customers
(
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    [Name]     NVARCHAR(100) NOT NULL,
    Email      NVARCHAR(255) NULL,
    CreatedAt  DATETIME2(0)  NOT NULL
               CONSTRAINT DF_Customers_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

/* Orders */
IF OBJECT_ID(N'dbo.Orders', N'U') IS NOT NULL DROP TABLE dbo.Orders;
GO
CREATE TABLE dbo.Orders
(
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId INT         NOT NULL,
    Payed      BIT         NOT NULL CONSTRAINT DF_Orders_Payed DEFAULT (0),
    Amount     DECIMAL(10,2) NULL,  -- null until payed
    CreatedAt  DATETIME2(0)  NOT NULL
               CONSTRAINT DF_Orders_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId)
        REFERENCES dbo.Customers(Id)
);

/* Enforce: Payed=0 -> Amount IS NULL; Payed=1 -> Amount IS NOT NULL */
ALTER TABLE dbo.Orders WITH NOCHECK
ADD CONSTRAINT CK_Orders_Payed_Amount_Nullability
CHECK ( (Payed = 0 AND Amount IS NULL) OR (Payed = 1 AND Amount IS NOT NULL) );
-- validate existing rows (none yet in a clean init)
ALTER TABLE dbo.Orders WITH CHECK CHECK CONSTRAINT CK_Orders_Payed_Amount_Nullability;
GO

/* Products (current, changeable price) */
IF OBJECT_ID(N'dbo.Products', N'U') IS NOT NULL DROP TABLE dbo.Products;
GO
CREATE TABLE dbo.Products
(
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    [Name]        NVARCHAR(200)  NOT NULL,
    CurrentPrice  DECIMAL(10,2)  NOT NULL,
    CreatedAt     DATETIME2(0)   NOT NULL
                  CONSTRAINT DF_Products_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Products_Name UNIQUE ([Name])
);
GO

/* OrderDetails (many-to-many + per-line price snapshot) */
IF OBJECT_ID(N'dbo.OrderDetails', N'U') IS NOT NULL DROP TABLE dbo.OrderDetails;
GO
CREATE TABLE dbo.OrderDetails
(
    OrderId    INT NOT NULL,
    ProductId  INT NOT NULL,
    Quantity   INT NOT NULL CONSTRAINT DF_OrderDetails_Quantity DEFAULT (1),
    Price      DECIMAL(10,2) NULL,   -- set when order is payed (snapshot)

    CONSTRAINT PK_OrderDetails PRIMARY KEY (OrderId, ProductId),

    CONSTRAINT FK_OrderDetails_Orders
        FOREIGN KEY (OrderId)   REFERENCES dbo.Orders(Id)   ON DELETE CASCADE,
    CONSTRAINT FK_OrderDetails_Products
        FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id)
);
GO

/* =========================
   Seed Data
   ========================= */
IF NOT EXISTS (SELECT 1 FROM dbo.Customers)
BEGIN
    INSERT INTO dbo.Customers ([Name], Email) VALUES
        (N'Alice',  N'alice@example.com'),
        (N'Bob',    N'bob@example.com'),
        (N'Clara',  NULL);
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products)
BEGIN
    INSERT INTO dbo.Products ([Name], CurrentPrice) VALUES
        (N'USB-C Cable',            9.99),
        (N'Wireless Mouse',        24.50),
        (N'Mechanical Keyboard',   79.00);
END

-- Example orders: one unpaid (Amount=NULL), one paid (Amount set)
IF NOT EXISTS (SELECT 1 FROM dbo.Orders)
BEGIN
    -- Unpaid order (Amount must remain NULL)
    INSERT INTO dbo.Orders (CustomerId, Payed, Amount)
    VALUES (1, 0, NULL);

    -- Paid order (Amount is required)
    INSERT INTO dbo.Orders (CustomerId, Payed, Amount)
    VALUES (2, 1, 99.99);
END

-- Example lines: for unpaid order, leave Price NULL; for paid order, snapshot
IF NOT EXISTS (SELECT 1 FROM dbo.OrderDetails)
BEGIN
    DECLARE @UnpaidOrderId INT = (SELECT TOP 1 Id FROM dbo.Orders WHERE Payed = 0 ORDER BY Id);
    DECLARE @PaidOrderId   INT = (SELECT TOP 1 Id FROM dbo.Orders WHERE Payed = 1 ORDER BY Id);
    DECLARE @Prod1 INT = (SELECT TOP 1 Id FROM dbo.Products ORDER BY Id);
    DECLARE @Prod2 INT = (SELECT TOP 1 Id FROM dbo.Products ORDER BY Id DESC);

    IF @UnpaidOrderId IS NOT NULL
        INSERT INTO dbo.OrderDetails (OrderId, ProductId, Quantity, Price)
        VALUES (@UnpaidOrderId, @Prod1, 1, NULL);

    IF @PaidOrderId IS NOT NULL
        INSERT INTO dbo.OrderDetails (OrderId, ProductId, Quantity, Price)
        SELECT @PaidOrderId, @Prod2, 2, CurrentPrice FROM dbo.Products WHERE Id = @Prod2;
END
GO

/* =========================
   Stored Procedures
   ========================= */
IF OBJECT_ID(N'dbo.GetCustomerOrdersTotalAfterDateAsResultSet', N'P') IS NOT NULL
    DROP PROCEDURE dbo.GetCustomerOrdersTotalAfterDateAsResultSet;
GO
CREATE PROCEDURE dbo.GetCustomerOrdersTotalAfterDateAsResultSet
    @CustomerID INT,
    @OrdersDate DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        ISNULL(SUM(o.Amount), 0) AS TotalAmount,
        COUNT(o.Id)              AS OrderCount
    FROM dbo.Orders AS o
    WHERE o.CustomerId = @CustomerID
      AND o.CreatedAt > @OrdersDate;
END
GO

IF OBJECT_ID(N'dbo.GetCustomerOrdersTotalAfterDateAsOutput', N'P') IS NOT NULL
    DROP PROCEDURE dbo.GetCustomerOrdersTotalAfterDateAsOutput;
GO
CREATE PROCEDURE dbo.GetCustomerOrdersTotalAfterDateAsOutput
    @CustomerID   INT,
    @OrdersDate   DATETIME2,
    @TotalAmount  DECIMAL(18,2) OUTPUT,
    @OrderCount   INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        @TotalAmount = ISNULL(SUM(o.Amount), 0),
        @OrderCount  = COUNT(o.Id)
    FROM dbo.Orders AS o
    WHERE o.CustomerId = @CustomerID
      AND o.CreatedAt > @OrdersDate;
END
GO

IF OBJECT_ID(N'dbo.GetCustomerOrdersTotalAfterDateWithStatus', N'P') IS NOT NULL
    DROP PROCEDURE dbo.GetCustomerOrdersTotalAfterDateWithStatus;
GO
CREATE PROCEDURE dbo.GetCustomerOrdersTotalAfterDateWithStatus
    @CustomerID   INT,
    @OrdersDate   DATETIME2,
    @TotalAmount  DECIMAL(18,2) OUTPUT,
    @OrderCount   INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Initialize outputs
    SET @TotalAmount = 0;
    SET @OrderCount  = 0;

    -- Status codes:
    --   0 = OK
    -- 404 = Customer not found
    IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Id = @CustomerID)
        RETURN 404;

    SELECT
        @TotalAmount = ISNULL(SUM(o.Amount), 0),
        @OrderCount  = COUNT(*)
    FROM dbo.Orders AS o
    WHERE o.CustomerId = @CustomerID
      AND o.CreatedAt  > @OrdersDate;

    RETURN 0;
END
GO

IF OBJECT_ID(N'dbo.GetCustomerOrdersTotalAfterDate_InOut', N'P') IS NOT NULL
    DROP PROCEDURE dbo.GetCustomerOrdersTotalAfterDate_InOut;
GO
CREATE PROCEDURE dbo.GetCustomerOrdersTotalAfterDate_InOut
    @CustomerID    INT,
    @OrdersDate    DATETIME2,
    @RunningTotal  DECIMAL(18,2) OUTPUT   -- IN/OUT: caller supplies initial value; proc updates it
AS
BEGIN
    SET NOCOUNT ON;

    -- Make sure NULL seeds are treated as 0
    SET @RunningTotal = ISNULL(@RunningTotal, 0);

    SELECT
        @RunningTotal = @RunningTotal + ISNULL(SUM(o.Amount), 0)
    FROM dbo.Orders AS o
    WHERE o.CustomerId = @CustomerID
      AND o.CreatedAt  > @OrdersDate;
END
GO

IF OBJECT_ID(N'dbo.GetCustomerOrderedProductsByEmail', N'P') IS NOT NULL
    DROP PROCEDURE dbo.GetCustomerOrderedProductsByEmail;
GO

CREATE PROCEDURE dbo.GetCustomerOrderedProductsByEmail
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    -- Return order lines with product info for the customer identified by email
    SELECT
        o.Id                         AS OrderId,
        o.CreatedAt                  AS OrderCreatedAt,
        o.Payed                      AS Payed,
        od.ProductId,
        p.[Name]                     AS ProductName,
        od.Quantity,
        COALESCE(od.Price, p.CurrentPrice)      AS UnitPriceUsed,
        CAST(od.Quantity * COALESCE(od.Price, p.CurrentPrice) AS DECIMAL(18,2)) AS LineTotal
    FROM dbo.Customers      AS c
    JOIN dbo.Orders         AS o  ON o.CustomerId = c.Id
    JOIN dbo.OrderDetails   AS od ON od.OrderId   = o.Id
    JOIN dbo.Products       AS p  ON p.Id         = od.ProductId
    WHERE c.Email = @Email
    ORDER BY o.CreatedAt, o.Id, od.ProductId;
END
GO

/* =========================
   Scalar Function
   ========================= */
IF OBJECT_ID(N'dbo.fn_GetCustomerOrdersTotalAfterDate', N'FN') IS NOT NULL
    DROP FUNCTION dbo.fn_GetCustomerOrdersTotalAfterDate;
GO
CREATE FUNCTION dbo.fn_GetCustomerOrdersTotalAfterDate
(
    @CustomerID INT,
    @OrdersDate DATETIME2
)
RETURNS DECIMAL(18,2)
AS
BEGIN
    DECLARE @Total DECIMAL(18,2);

    SELECT @Total = ISNULL(SUM(o.Amount), 0)
    FROM dbo.Orders AS o
    WHERE o.CustomerId = @CustomerID
      AND o.CreatedAt  > @OrdersDate;

    RETURN @Total;
END
GO