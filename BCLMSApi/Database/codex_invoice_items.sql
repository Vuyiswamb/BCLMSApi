IF OBJECT_ID('dbo.InvoiceItems', 'U') IS NULL
CREATE TABLE dbo.InvoiceItems
(
    InvoiceItemId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InvoiceItems PRIMARY KEY,
    ItemCode NVARCHAR(50) NOT NULL CONSTRAINT UQ_InvoiceItems_ItemCode UNIQUE,
    Description NVARCHAR(250) NOT NULL,
    GlAccountNumber NVARCHAR(50) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_InvoiceItems_IsActive DEFAULT(1),
    CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_InvoiceItems_CreatedDate DEFAULT(SYSUTCDATETIME()),
    ModifiedDate DATETIME2(0) NULL
);

IF NOT EXISTS (SELECT 1 FROM dbo.InvoiceItems WHERE ItemCode = 'Item 1')
INSERT dbo.InvoiceItems (ItemCode, Description, GlAccountNumber, Amount) VALUES ('Item 1', 'Item 1', '000000000081171206861', 948.00);
IF NOT EXISTS (SELECT 1 FROM dbo.InvoiceItems WHERE ItemCode = 'Item 2')
INSERT dbo.InvoiceItems (ItemCode, Description, GlAccountNumber, Amount) VALUES ('Item 2', 'Item 2', '000000000081171206861', 5968.00);
