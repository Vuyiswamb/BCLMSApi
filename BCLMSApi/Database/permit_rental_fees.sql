IF OBJECT_ID('dbo.PermitRentalFees', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PermitRentalFees
    (
        PermitRentalFeeId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PermitRentalFees PRIMARY KEY,
        BusinessType NVARCHAR(120) NOT NULL,
        TradingLocation NVARCHAR(200) NULL,
        MonthlyFee DECIMAL(18,2) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_PermitRentalFees_IsActive DEFAULT (1),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_PermitRentalFees_CreatedDate DEFAULT (SYSUTCDATETIME()),
        ModifiedDate DATETIME2(0) NULL,
        CONSTRAINT CK_PermitRentalFees_MonthlyFee CHECK (MonthlyFee >= 0)
    );
END;
GO

-- Schedule 23: monthly tariffs for informal trade stalls.  Location records override the business-type default.
MERGE dbo.PermitRentalFees AS target
USING (VALUES
    ('Perishable Goods', NULL, 113.00),
    ('Cell phone Accessories & Airtime Business', NULL, 113.00),
    ('None -perishable Goods', NULL, 113.00),
    ('None -perishable Goods', 'Atteridgeville Centre Mall', 76.00),
    ('Perishable Goods', 'Zithobeni trading areas', 56.00),
    ('Cell phone Accessories & Airtime Business', 'Zithobeni trading areas', 56.00),
    ('None -perishable Goods', 'Zithobeni trading areas', 56.00)
) AS source (BusinessType, TradingLocation, MonthlyFee)
ON target.BusinessType = source.BusinessType
   AND ISNULL(target.TradingLocation, '') = ISNULL(source.TradingLocation, '')
WHEN MATCHED THEN UPDATE SET MonthlyFee = source.MonthlyFee, IsActive = 1, ModifiedDate = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (BusinessType, TradingLocation, MonthlyFee) VALUES (source.BusinessType, source.TradingLocation, source.MonthlyFee);
GO

CREATE OR ALTER PROCEDURE dbo.usp_PermitRentalFees_Get
    @BusinessType NVARCHAR(120),
    @TradingLocation NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) PermitRentalFeeId, BusinessType, TradingLocation, MonthlyFee, IsActive
    FROM dbo.PermitRentalFees
    WHERE IsActive = 1
      AND BusinessType = @BusinessType
      AND (TradingLocation IS NULL OR TradingLocation = NULLIF(@TradingLocation, ''))
    ORDER BY CASE WHEN TradingLocation = NULLIF(@TradingLocation, '') THEN 0 ELSE 1 END;
END;
GO
