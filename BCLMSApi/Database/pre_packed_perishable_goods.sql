/* Run once against the BCLMS database before deploying the API change. */
IF COL_LENGTH('dbo.Applications', 'PrePackedPerishableGoods') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD PrePackedPerishableGoods NVARCHAR(1000) NULL;
END;
GO
