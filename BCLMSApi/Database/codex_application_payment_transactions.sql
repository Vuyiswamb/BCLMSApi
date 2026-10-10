IF OBJECT_ID('dbo.ApplicationPaymentTransactions', 'U') IS NULL
CREATE TABLE dbo.ApplicationPaymentTransactions
(
    ApplicationPaymentTransactionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApplicationPaymentTransactions PRIMARY KEY,
    ApplicationId INT NOT NULL,
    PaymentReference NVARCHAR(100) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    PaymentMonth DATE NOT NULL,
    PaymentStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_ApplicationPaymentTransactions_Status DEFAULT('Cleared'),
    RecordedDate DATETIME2(0) NOT NULL CONSTRAINT DF_ApplicationPaymentTransactions_RecordedDate DEFAULT(SYSUTCDATETIME()),
    RecordedByUserId INT NULL,
    CONSTRAINT UQ_ApplicationPaymentTransactions_ApplicationMonth UNIQUE (ApplicationId, PaymentMonth)
);
