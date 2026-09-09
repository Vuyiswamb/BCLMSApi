SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM dbo.Groups WHERE GroupName = 'Office Support')
    INSERT dbo.Groups (GroupName, Description, IsActive)
    VALUES ('Office Support', 'Register walk-in customers, record workshop attendance and submit assisted applications.', 1);

IF OBJECT_ID('dbo.AssistedIntakes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AssistedIntakes (
        IntakeId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        OperatorUserId INT NOT NULL REFERENCES dbo.Users(UserId),
        CustomerUserId INT NOT NULL REFERENCES dbo.Users(UserId),
        IdNumber NVARCHAR(80) NOT NULL,
        Phone NVARCHAR(40) NOT NULL,
        BusinessId INT NULL REFERENCES dbo.CustomerBusinesses(BusinessId),
        WorkshopRecordedAtUtc DATETIME2 NULL,
        ApplicationId INT NULL REFERENCES dbo.Applications(ApplicationId),
        ApplicationResponse NVARCHAR(MAX) NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_AssistedIntakes_Operator ON dbo.AssistedIntakes(OperatorUserId, IntakeId);
END;
COMMIT;

-- Preserve the deployed business queries and add the attendance fields they omit.
-- The owner and active-business filters remain exactly as deployed.
DECLARE @ProcedureName NVARCHAR(200), @Definition NVARCHAR(MAX), @CreateAt INT, @ProcedureAt INT;
DECLARE BusinessReads CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM sys.procedures WHERE schema_id = SCHEMA_ID('dbo')
        AND name IN ('usp_CustomerBusinesses_GetById', 'usp_CustomerBusinesses_GetForUser');
OPEN BusinessReads;
FETCH NEXT FROM BusinessReads INTO @ProcedureName;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Definition = OBJECT_DEFINITION(OBJECT_ID('dbo.' + @ProcedureName));
    IF CHARINDEX('AS WorkshopAttended', @Definition) = 0 AND CHARINDEX('businesses.WorkshopAttended', @Definition) = 0
    BEGIN
        IF CHARINDEX('businesses.BusinessId,', @Definition) = 0
            THROW 51010, 'Business read procedure differs from expected schema; review attendance migration.', 1;
        SET @Definition = REPLACE(@Definition, 'businesses.BusinessId,',
            'businesses.BusinessId, CONVERT(BIT, COALESCE(businesses.WorkshopAttended, 0)) AS WorkshopAttended, businesses.WorkshopAttendedDate,');
        SET @CreateAt = CHARINDEX('CREATE', UPPER(@Definition));
        SET @ProcedureAt = CHARINDEX('PROCEDURE', UPPER(@Definition));
        IF @CreateAt > 0 AND @CreateAt < @ProcedureAt
            SET @Definition = STUFF(@Definition, @CreateAt, @ProcedureAt - @CreateAt, 'ALTER ');
        EXEC sys.sp_executesql @Definition;
    END;
    FETCH NEXT FROM BusinessReads INTO @ProcedureName;
END;
CLOSE BusinessReads;
DEALLOCATE BusinessReads;
