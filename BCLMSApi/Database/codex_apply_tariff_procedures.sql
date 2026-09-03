CREATE OR ALTER PROCEDURE dbo.usp_Tariffs_GetActive
    @LicenceType NVARCHAR(100),
    @ApplicationKind NVARCHAR(40) = 'New'
AS
BEGIN
    SET NOCOUNT ON;

    SET @LicenceType = LTRIM(RTRIM(@LicenceType));
    SET @ApplicationKind = LTRIM(RTRIM(ISNULL(@ApplicationKind, 'New')));

    IF @ApplicationKind = '' OR @ApplicationKind = 'New Application'
    BEGIN
        SET @ApplicationKind = 'New';
    END;

    IF @LicenceType = 'Formal Business - New Application'
    BEGIN
        SET @LicenceType = 'Formal Business';
        SET @ApplicationKind = 'New';
    END;

    IF @LicenceType = 'Formal Business - Renewal'
    BEGIN
        SET @LicenceType = 'Formal Business';
        SET @ApplicationKind = 'Renewal';
    END;

    SELECT TOP (1)
        TariffId,
        LicenceType,
        ApplicationKind,
        FeeAmount,
        IsActive
    FROM dbo.Tariffs
    WHERE LicenceType = @LicenceType
      AND ApplicationKind = @ApplicationKind
      AND IsActive = 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Tariffs_GetAll
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        TariffId,
        LicenceType,
        ApplicationKind,
        FeeAmount,
        IsActive
    FROM dbo.Tariffs
    ORDER BY LicenceType, ApplicationKind;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Tariffs_Save
    @LicenceType NVARCHAR(100),
    @ApplicationKind NVARCHAR(40) = 'New',
    @FeeAmount DECIMAL(18,2),
    @IsActive BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TariffId INT;

    SET @LicenceType = LTRIM(RTRIM(@LicenceType));
    SET @ApplicationKind = LTRIM(RTRIM(ISNULL(@ApplicationKind, 'New')));

    IF @ApplicationKind = ''
    BEGIN
        SET @ApplicationKind = 'New';
    END;

    SELECT @TariffId = TariffId
    FROM dbo.Tariffs
    WHERE LicenceType = @LicenceType
      AND ApplicationKind = @ApplicationKind;

    UPDATE dbo.Tariffs
    SET FeeAmount = @FeeAmount,
        IsActive = @IsActive,
        ModifiedDate = SYSUTCDATETIME()
    WHERE TariffId = @TariffId;

    IF @@ROWCOUNT = 0
    BEGIN
        BEGIN TRY
            INSERT INTO dbo.Tariffs (LicenceType, ApplicationKind, FeeAmount, IsActive)
            VALUES (@LicenceType, @ApplicationKind, @FeeAmount, @IsActive);

            SET @TariffId = SCOPE_IDENTITY();
        END TRY
        BEGIN CATCH
            IF ERROR_NUMBER() NOT IN (2601, 2627)
            BEGIN
                THROW;
            END;

            SELECT @TariffId = TariffId
            FROM dbo.Tariffs
            WHERE LicenceType = @LicenceType
              AND ApplicationKind = @ApplicationKind;

            UPDATE dbo.Tariffs
            SET FeeAmount = @FeeAmount,
                IsActive = @IsActive,
                ModifiedDate = SYSUTCDATETIME()
            WHERE TariffId = @TariffId;
        END CATCH;
    END;

    SELECT
        TariffId,
        LicenceType,
        ApplicationKind,
        FeeAmount,
        IsActive
    FROM dbo.Tariffs
    WHERE TariffId = @TariffId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Tariffs_Update
    @TariffId INT,
    @LicenceType NVARCHAR(100),
    @ApplicationKind NVARCHAR(40) = 'New',
    @FeeAmount DECIMAL(18,2),
    @IsActive BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ExistingTariffId INT;

    SET @LicenceType = LTRIM(RTRIM(@LicenceType));
    SET @ApplicationKind = LTRIM(RTRIM(ISNULL(@ApplicationKind, 'New')));

    IF @ApplicationKind = ''
    BEGIN
        SET @ApplicationKind = 'New';
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Tariffs WHERE TariffId = @TariffId)
    BEGIN
        RETURN;
    END;

    SELECT @ExistingTariffId = TariffId
    FROM dbo.Tariffs
    WHERE LicenceType = @LicenceType
      AND ApplicationKind = @ApplicationKind;

    IF @ExistingTariffId IS NOT NULL AND @ExistingTariffId <> @TariffId
    BEGIN
        UPDATE dbo.Tariffs
        SET FeeAmount = @FeeAmount,
            IsActive = @IsActive,
            ModifiedDate = SYSUTCDATETIME()
        WHERE TariffId = @ExistingTariffId;

        SELECT
            TariffId,
            LicenceType,
            ApplicationKind,
            FeeAmount,
            IsActive
        FROM dbo.Tariffs
        WHERE TariffId = @ExistingTariffId;

        RETURN;
    END;

    UPDATE dbo.Tariffs
    SET LicenceType = @LicenceType,
        ApplicationKind = @ApplicationKind,
        FeeAmount = @FeeAmount,
        IsActive = @IsActive,
        ModifiedDate = SYSUTCDATETIME()
    WHERE TariffId = @TariffId;

    SELECT
        TariffId,
        LicenceType,
        ApplicationKind,
        FeeAmount,
        IsActive
    FROM dbo.Tariffs
    WHERE TariffId = @TariffId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Tariffs_Disable
    @TariffId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Tariffs
    SET IsActive = 0,
        ModifiedDate = SYSUTCDATETIME()
    WHERE TariffId = @TariffId;

    SELECT CAST(CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS BIT) AS Disabled;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Tariffs_Delete
    @TariffId INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.Tariffs
    WHERE TariffId = @TariffId;

    SELECT CAST(CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS BIT) AS Deleted;
END;
GO
