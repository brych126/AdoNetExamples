IF DB_ID(N'AdoNetExamples') IS NULL
BEGIN
    CREATE DATABASE [AdoNetExamples];
END
GO

USE [AdoNetExamples];
GO

-- Create Customers table
IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers
    (
        Id         INT IDENTITY(1,1) PRIMARY KEY,
        [Name]     NVARCHAR(100) NOT NULL,
        Email      NVARCHAR(255) NULL,
        CreatedAt  DATETIME2(0) NOT NULL 
                   CONSTRAINT DF_Customers_CreatedAt DEFAULT (SYSUTCDATETIME())
    );
END
GO

-- Create Orders table (linked to Customers)
IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Orders
    (
        Id        INT IDENTITY(1,1) PRIMARY KEY,
        CustomerId INT NOT NULL,
        Amount    DECIMAL(10,2) NOT NULL,
        CreatedAt DATETIME2(0) NOT NULL 
                   CONSTRAINT DF_Orders_CreatedAt DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId) 
            REFERENCES dbo.Customers(Id)
    );
END
GO

-- Seed Customers
IF NOT EXISTS (SELECT 1 FROM dbo.Customers)
BEGIN
    INSERT INTO dbo.Customers ([Name], Email) VALUES
        (N'Alice',  N'alice@example.com'),
        (N'Bob',    N'bob@example.com'),
        (N'Clara',  NULL);
END
GO

-- Seed Orders
IF NOT EXISTS (SELECT 1 FROM dbo.Orders)
BEGIN
    INSERT INTO dbo.Orders (CustomerId, Amount) VALUES
        (1, 120.50),
        (2,  99.99),
        (1,  49.90);
END
GO

-- Stored procedures
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

-- Functions
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