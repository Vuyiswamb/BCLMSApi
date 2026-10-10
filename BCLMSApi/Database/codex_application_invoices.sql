CREATE TABLE dbo.ApplicationInvoices
(
  ApplicationInvoiceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApplicationInvoices PRIMARY KEY,
  ApplicationId INT NOT NULL CONSTRAINT UQ_ApplicationInvoices_ApplicationId UNIQUE,
  PaymentReference NVARCHAR(100) NOT NULL,
  GlAccountNumber NVARCHAR(50) NOT NULL,
  PaymentStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_ApplicationInvoices_Status DEFAULT('Outstanding'),
  CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_ApplicationInvoices_Created DEFAULT(SYSUTCDATETIME()),
  ConfirmedDate DATETIME2(0) NULL,
  ConfirmedByUserId INT NULL
);
CREATE TABLE dbo.ApplicationInvoiceLines
(
  ApplicationInvoiceLineId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApplicationInvoiceLines PRIMARY KEY,
  ApplicationInvoiceId INT NOT NULL CONSTRAINT FK_ApplicationInvoiceLines_Invoice FOREIGN KEY REFERENCES dbo.ApplicationInvoices(ApplicationInvoiceId),
  ItemCode NVARCHAR(50) NOT NULL,
  Description NVARCHAR(250) NOT NULL,
  Amount DECIMAL(18,2) NOT NULL
);
