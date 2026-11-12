IF OBJECT_ID('dbo.BankMePaymentAttempts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BankMePaymentAttempts
    (
        BankMePaymentAttemptId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BankMePaymentAttempts PRIMARY KEY,
        ApplicationId INT NOT NULL,
        GroupReference NVARCHAR(100) NOT NULL CONSTRAINT UQ_BankMePaymentAttempts_GroupReference UNIQUE,
        UniqueReference NVARCHAR(100) NOT NULL CONSTRAINT UQ_BankMePaymentAttempts_UniqueReference UNIQUE,
        Amount DECIMAL(18,2) NOT NULL,
        PaymentUuid NVARCHAR(100) NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_BankMePaymentAttempts_Status DEFAULT('Started'),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_BankMePaymentAttempts_CreatedDate DEFAULT(SYSUTCDATETIME()),
        UpdatedDate DATETIME2(0) NULL,
        WebhookReceivedDate DATETIME2(0) NULL
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_BankMePaymentAttempts_Save
    @ApplicationId INT,
    @GroupReference NVARCHAR(100),
    @UniqueReference NVARCHAR(100),
    @Amount DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.BankMePaymentAttempts(ApplicationId, GroupReference, UniqueReference, Amount)
    VALUES(@ApplicationId, @GroupReference, @UniqueReference, @Amount);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_BankMePaymentAttempts_Get
    @GroupReference NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT BankMePaymentAttemptId, ApplicationId, GroupReference, UniqueReference, Amount, PaymentUuid, Status
    FROM dbo.BankMePaymentAttempts
    WHERE GroupReference = @GroupReference;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_BankMePaymentAttempts_Complete
    @GroupReference NVARCHAR(100),
    @PaymentUuid NVARCHAR(100),
    @PaymentStatus NVARCHAR(30),
    @GatewayAmount DECIMAL(18,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ApplicationId INT, @Amount DECIMAL(18,2), @InvoiceAmount DECIMAL(18,2), @CurrentStatus NVARCHAR(30), @Reference NVARCHAR(100);
    SELECT @ApplicationId = ApplicationId, @Amount = Amount, @CurrentStatus = Status
    FROM dbo.BankMePaymentAttempts WHERE GroupReference = @GroupReference;
    IF @ApplicationId IS NULL RETURN;

    IF @PaymentStatus IN ('Paid', 'Success', 'Successful', 'Confirmed', 'Complete', 'Completed')
    BEGIN
        SELECT @InvoiceAmount = SUM(l.Amount), @Reference = MAX(i.PaymentReference)
        FROM dbo.ApplicationInvoiceLines l JOIN dbo.ApplicationInvoices i ON i.ApplicationInvoiceId = l.ApplicationInvoiceId
        WHERE i.ApplicationId = @ApplicationId;
        IF @InvoiceAmount IS NULL OR ABS(@InvoiceAmount - @Amount) > 0.01 RETURN;
        IF @GatewayAmount IS NOT NULL AND ABS(@GatewayAmount - @Amount) > 0.01 RETURN;

        UPDATE dbo.BankMePaymentAttempts SET PaymentUuid = @PaymentUuid, Status = 'Confirmed', UpdatedDate = SYSUTCDATETIME(), WebhookReceivedDate = SYSUTCDATETIME()
        WHERE GroupReference = @GroupReference AND Status <> 'Confirmed';
        UPDATE dbo.ApplicationInvoices SET PaymentStatus = 'Confirmed', ConfirmedDate = SYSUTCDATETIME(), ConfirmedByUserId = NULL
        WHERE ApplicationId = @ApplicationId AND PaymentStatus <> 'Confirmed';
        INSERT dbo.ApplicationPaymentTransactions(ApplicationId, PaymentReference, Amount, PaymentMonth, PaymentStatus, RecordedByUserId)
        SELECT @ApplicationId, @Reference, @InvoiceAmount, DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1), 'Cleared', NULL
        WHERE NOT EXISTS (SELECT 1 FROM dbo.ApplicationPaymentTransactions WHERE ApplicationId = @ApplicationId AND PaymentMonth = DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1));
    END
    ELSE
        UPDATE dbo.BankMePaymentAttempts SET PaymentUuid = @PaymentUuid, Status = LEFT(@PaymentStatus, 30), UpdatedDate = SYSUTCDATETIME(), WebhookReceivedDate = SYSUTCDATETIME()
        WHERE GroupReference = @GroupReference AND Status <> 'Confirmed';
END;
GO
