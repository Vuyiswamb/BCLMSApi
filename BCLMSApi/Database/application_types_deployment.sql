SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.Applications', 'ApplicationFee') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD ApplicationFee DECIMAL(18,2) NULL;
END;
GO

IF COL_LENGTH('dbo.Applications', 'PostalAddress') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD PostalAddress NVARCHAR(300) NULL;
END;
GO

MERGE dbo.BusinessTypes AS target
USING
(
    VALUES
        ('Formal Business', 'Formal business licence'),
        ('Formal Business - New Application', 'Formal business licence new application'),
        ('Formal Business - Renewal', 'Formal business licence renewal'),
        ('Hawkers Licence', 'Hawker trading licence'),
        ('Trade Stand Permit', 'Trade stand permit'),
        ('Food Vending Licence', 'Food vending licence'),
        ('Event Licence', 'Event licence')
) AS source (BusinessTypeName, Description)
ON target.BusinessTypeName = source.BusinessTypeName
WHEN MATCHED THEN
    UPDATE SET Description = source.Description, IsActive = 1
WHEN NOT MATCHED THEN
    INSERT (BusinessTypeName, Description) VALUES (source.BusinessTypeName, source.Description);
GO

UPDATE dbo.BusinessTypes
SET IsActive = 0
WHERE BusinessTypeName IN
(
    'Informal Business / Hawkers Licence',
    'Food Vending Permit',
    'Food Vending',
    'Event Trading Application'
);
GO

CREATE OR ALTER PROCEDURE dbo.usp_Applications_BusinessHasApplication
    @UserId INT,
    @BusinessId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CAST(CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.Applications applications
        WHERE applications.UserId = @UserId
          AND applications.BusinessId = @BusinessId
          AND applications.Archive_Date IS NULL
    )
    THEN 1 ELSE 0 END AS BIT) AS ExistsFlag;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Applications_Insert
    @ApplicantName NVARCHAR(150),
    @UserId INT,
    @BusinessId INT,
    @IdOrPassportNumber NVARCHAR(50),
    @EmailAddress NVARCHAR(150),
    @MobileNumber NVARCHAR(40),
    @BusinessName NVARCHAR(200),
    @RegistrationNumber NVARCHAR(80) = NULL,
    @LicenceType NVARCHAR(100),
    @ApplicationFee DECIMAL(18,2) = NULL,
    @WardNumber NVARCHAR(20) = NULL,
    @TownshipId INT,
    @PhysicalAddress NVARCHAR(300),
    @PostalAddress NVARCHAR(300) = NULL,
    @Notes NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ApplicationId INT;
    DECLARE @TrackingNumber NVARCHAR(30);
    DECLARE @BusinessTypeId INT;
    DECLARE @SubmittedStatusId INT;

    SELECT @BusinessTypeId = BusinessTypeId
    FROM dbo.BusinessTypes
    WHERE BusinessTypeName = @LicenceType;

    IF @BusinessTypeId IS NULL
    BEGIN
        INSERT dbo.BusinessTypes (BusinessTypeName, Description)
        VALUES (@LicenceType, 'Created from public application submission');

        SET @BusinessTypeId = CONVERT(INT, SCOPE_IDENTITY());
    END;

    SELECT @SubmittedStatusId = Application_Status_id
    FROM dbo.lu_application_status
    WHERE StatusName = 'Submitted';

    INSERT dbo.Applications
    (
        TrackingNumber,
        ApplicantName,
        UserId,
        BusinessId,
        IdOrPassportNumber,
        EmailAddress,
        MobileNumber,
        BusinessName,
        RegistrationNumber,
        BusinessTypeId,
        LicenceType,
        ApplicationFee,
        Application_Status_id,
        TownshipId,
        WardNumber,
        AreaOrSuburb,
        PhysicalAddress,
        PostalAddress,
        Notes
    )
    VALUES
    (
        CONCAT('P-', RIGHT(REPLACE(CONVERT(NVARCHAR(36), NEWID()), '-', ''), 28)),
        @ApplicantName,
        @UserId,
        @BusinessId,
        @IdOrPassportNumber,
        @EmailAddress,
        @MobileNumber,
        @BusinessName,
        NULLIF(@RegistrationNumber, ''),
        @BusinessTypeId,
        @LicenceType,
        @ApplicationFee,
        @SubmittedStatusId,
        @TownshipId,
        NULLIF(@WardNumber, ''),
        NULL,
        @PhysicalAddress,
        NULLIF(@PostalAddress, ''),
        NULLIF(@Notes, '')
    );

    SET @ApplicationId = CONVERT(INT, SCOPE_IDENTITY());
    SET @TrackingNumber = CONCAT('BCLMS-', YEAR(SYSUTCDATETIME()), '-', RIGHT(CONCAT('0000', @ApplicationId), 4));

    UPDATE dbo.Applications
    SET TrackingNumber = @TrackingNumber
    WHERE ApplicationId = @ApplicationId;

    SELECT
        ApplicationId,
        TrackingNumber,
        ApplicantName,
        IdOrPassportNumber,
        EmailAddress,
        MobileNumber,
        BusinessName,
        RegistrationNumber,
        COALESCE(businessTypes.BusinessTypeName, applications.LicenceType) AS LicenceType,
        applications.ApplicationFee,
        TownshipId,
        WardNumber,
        AreaOrSuburb,
        PhysicalAddress,
        PostalAddress,
        Notes,
        CurrentStage,
        COALESCE(statuses.StatusName, applications.Status) AS Status,
        SubmittedDate
    FROM dbo.Applications applications
    LEFT JOIN dbo.BusinessTypes businessTypes ON businessTypes.BusinessTypeId = applications.BusinessTypeId
    LEFT JOIN dbo.lu_application_status statuses ON statuses.Application_Status_id = applications.Application_Status_id
    WHERE applications.ApplicationId = @ApplicationId;
END;
GO
