IF COL_LENGTH('dbo.Applications', 'BusinessId') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD BusinessId INT NULL;
END;
GO

IF OBJECT_ID('dbo.CustomerBusinesses', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerBusinesses
    (
        BusinessId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerBusinesses PRIMARY KEY,
        UserId INT NOT NULL,
        BusinessName NVARCHAR(200) NOT NULL,
        RegistrationNumber NVARCHAR(80) NULL,
        TownshipId INT NULL,
        WardNumber NVARCHAR(20) NULL,
        PhysicalAddress NVARCHAR(300) NOT NULL,
        Notes NVARCHAR(1000) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_CustomerBusinesses_IsActive DEFAULT (1),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_CustomerBusinesses_CreatedDate DEFAULT (SYSUTCDATETIME()),
        ModifiedDate DATETIME2(0) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CustomerBusinesses_Users')
BEGIN
    ALTER TABLE dbo.CustomerBusinesses
    ADD CONSTRAINT FK_CustomerBusinesses_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CustomerBusinesses_Townships')
BEGIN
    ALTER TABLE dbo.CustomerBusinesses
    ADD CONSTRAINT FK_CustomerBusinesses_Townships FOREIGN KEY (TownshipId) REFERENCES dbo.Townships(TownshipId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Applications_CustomerBusinesses')
BEGIN
    ALTER TABLE dbo.Applications
    ADD CONSTRAINT FK_Applications_CustomerBusinesses FOREIGN KEY (BusinessId) REFERENCES dbo.CustomerBusinesses(BusinessId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Applications_BusinessId' AND object_id = OBJECT_ID('dbo.Applications'))
BEGIN
    CREATE INDEX IX_Applications_BusinessId ON dbo.Applications(BusinessId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CustomerBusinesses_UserId' AND object_id = OBJECT_ID('dbo.CustomerBusinesses'))
BEGIN
    CREATE INDEX IX_CustomerBusinesses_UserId ON dbo.CustomerBusinesses(UserId, IsActive);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_CustomerBusinesses_GetForUser
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        businesses.BusinessId,
        businesses.UserId,
        businesses.BusinessName,
        businesses.RegistrationNumber,
        businesses.TownshipId,
        townships.TownshipName,
        businesses.WardNumber,
        businesses.PhysicalAddress,
        businesses.Notes,
        businesses.IsActive,
        businesses.CreatedDate
    FROM dbo.CustomerBusinesses businesses
    LEFT JOIN dbo.Townships townships ON townships.TownshipId = businesses.TownshipId
    WHERE businesses.UserId = @UserId
      AND businesses.IsActive = 1
    ORDER BY businesses.BusinessName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_CustomerBusinesses_GetById
    @UserId INT,
    @BusinessId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        businesses.BusinessId,
        businesses.UserId,
        businesses.BusinessName,
        businesses.RegistrationNumber,
        businesses.TownshipId,
        townships.TownshipName,
        businesses.WardNumber,
        businesses.PhysicalAddress,
        businesses.Notes,
        businesses.IsActive,
        businesses.CreatedDate
    FROM dbo.CustomerBusinesses businesses
    LEFT JOIN dbo.Townships townships ON townships.TownshipId = businesses.TownshipId
    WHERE businesses.UserId = @UserId
      AND businesses.BusinessId = @BusinessId
      AND businesses.IsActive = 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_CustomerBusinesses_Create
    @UserId INT,
    @BusinessName NVARCHAR(200),
    @RegistrationNumber NVARCHAR(80) = NULL,
    @TownshipId INT = NULL,
    @WardNumber NVARCHAR(20) = NULL,
    @PhysicalAddress NVARCHAR(300),
    @Notes NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CreatedBusiness TABLE (BusinessId INT NOT NULL);

    INSERT dbo.CustomerBusinesses
    (
        UserId,
        BusinessName,
        RegistrationNumber,
        TownshipId,
        WardNumber,
        PhysicalAddress,
        Notes
    )
    OUTPUT INSERTED.BusinessId INTO @CreatedBusiness(BusinessId)
    VALUES
    (
        @UserId,
        @BusinessName,
        NULLIF(@RegistrationNumber, ''),
        @TownshipId,
        NULLIF(@WardNumber, ''),
        @PhysicalAddress,
        NULLIF(@Notes, '')
    );

    SELECT
        businesses.BusinessId,
        businesses.UserId,
        businesses.BusinessName,
        businesses.RegistrationNumber,
        businesses.TownshipId,
        townships.TownshipName,
        businesses.WardNumber,
        businesses.PhysicalAddress,
        businesses.Notes,
        businesses.IsActive,
        businesses.CreatedDate
    FROM dbo.CustomerBusinesses businesses
    INNER JOIN @CreatedBusiness created ON created.BusinessId = businesses.BusinessId
    LEFT JOIN dbo.Townships townships ON townships.TownshipId = businesses.TownshipId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_CustomerBusinesses_Update
    @BusinessId INT,
    @UserId INT,
    @BusinessName NVARCHAR(200),
    @RegistrationNumber NVARCHAR(80) = NULL,
    @TownshipId INT = NULL,
    @WardNumber NVARCHAR(20) = NULL,
    @PhysicalAddress NVARCHAR(300),
    @Notes NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.CustomerBusinesses
    SET
        BusinessName = @BusinessName,
        RegistrationNumber = NULLIF(@RegistrationNumber, ''),
        TownshipId = @TownshipId,
        WardNumber = NULLIF(@WardNumber, ''),
        PhysicalAddress = @PhysicalAddress,
        Notes = NULLIF(@Notes, ''),
        ModifiedDate = SYSUTCDATETIME()
    WHERE BusinessId = @BusinessId
      AND UserId = @UserId
      AND IsActive = 1;

    EXEC dbo.usp_CustomerBusinesses_GetById @UserId = @UserId, @BusinessId = @BusinessId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_CustomerBusinesses_Deactivate
    @UserId INT,
    @BusinessId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.CustomerBusinesses
    SET IsActive = 0,
        ModifiedDate = SYSUTCDATETIME()
    WHERE BusinessId = @BusinessId
      AND UserId = @UserId
      AND IsActive = 1;

    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO
