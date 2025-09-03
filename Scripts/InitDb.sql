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