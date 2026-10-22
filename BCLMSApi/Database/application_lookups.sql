IF OBJECT_ID('dbo.ApplicationLookups', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApplicationLookups
    (
        LookupId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApplicationLookups PRIMARY KEY,
        LookupType NVARCHAR(50) NOT NULL,
        LookupValue NVARCHAR(250) NOT NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_ApplicationLookups_SortOrder DEFAULT (100),
        IsActive BIT NOT NULL CONSTRAINT DF_ApplicationLookups_IsActive DEFAULT (1),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_ApplicationLookups_CreatedDate DEFAULT SYSUTCDATETIME(),
        ModifiedDate DATETIME2(0) NULL,
        CONSTRAINT UQ_ApplicationLookups_TypeValue UNIQUE (LookupType, LookupValue)
    );
END;
GO

MERGE dbo.ApplicationLookups AS target
USING (VALUES
 ('BusinessType','None -perishable Goods'),('BusinessType','Perishable Goods'),('BusinessType','Cell phone Accessories & Airtime Business'),('BusinessType','Photographer'),
 ('PerishableGoods','Fat cake'),('PerishableGoods','Scones'),('PerishableGoods','Snacks'),('PerishableGoods','Fruits & Vegetables'),('PerishableGoods','Soft drinks'),('PerishableGoods','Mealies'),('PerishableGoods','Breakfast, Boerevors rolls (Prepared)'),('PerishableGoods','Desserts, Hot vegetables (Prepared)'),('PerishableGoods','Pap & Vleis (Prepared)'),('PerishableGoods','Samosas and Soft drinks (Prepared)'),
 ('PrePackedPerishableGoods','Achaar and Eggs'),('PrePackedPerishableGoods','Boiled Mealie and nuts'),('PrePackedPerishableGoods','Dried Fruits and Vegetables'),('PrePackedPerishableGoods','Ice Cream'),('PrePackedPerishableGoods','Fresh Fruits & Vegetables'),('PrePackedPerishableGoods','Fresh Flowers'),('PrePackedPerishableGoods','Soft drinks and snacks'),('PrePackedPerishableGoods','Pop corn'),('PrePackedPerishableGoods','Mopane Worms')
) AS source (LookupType, LookupValue)
ON target.LookupType=source.LookupType AND target.LookupValue=source.LookupValue
WHEN MATCHED THEN UPDATE SET IsActive=1
WHEN NOT MATCHED THEN INSERT (LookupType,LookupValue) VALUES (source.LookupType,source.LookupValue);
GO
