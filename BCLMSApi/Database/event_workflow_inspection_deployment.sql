SET XACT_ABORT ON;
BEGIN TRANSACTION;
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
    @TradeStandBusinessType NVARCHAR(120) = NULL,
    @EventStartDate DATE = NULL,
    @EventEndDate DATE = NULL,
    @ApplicationFee DECIMAL(18,2) = NULL,
    @WardNumber NVARCHAR(20) = NULL,
    @RegionName NVARCHAR(120) = NULL,
    @AreaCategory NVARCHAR(80) = NULL,
    @TownshipId INT,
    @PhysicalAddress NVARCHAR(300),
    @PostalAddress NVARCHAR(300) = NULL,
    @Latitude DECIMAL(9,6) = NULL,
    @Longitude DECIMAL(9,6) = NULL,
    @Notes NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ApplicationId INT;
    DECLARE @TrackingNumber NVARCHAR(30);
    DECLARE @BusinessTypeId INT;
    DECLARE @SubmittedStatusId INT;
    DECLARE @ComplianceGroupName NVARCHAR(80);
    DECLARE @IsFormalBusiness BIT;

    SET @IsFormalBusiness = CASE WHEN @LicenceType LIKE 'Formal Business%' THEN 1 ELSE 0 END;

    SET @ComplianceGroupName = CASE
        WHEN @AreaCategory LIKE '%Non Restricted%'
          OR @AreaCategory LIKE '%Non-Restricted%'
          OR @AreaCategory LIKE '%Non Declared%'
          OR @AreaCategory LIKE '%Non-Declared%'
            THEN 'Metro Police'
        ELSE 'Compliance Officer'
    END;

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
        TradeStandBusinessType,
        EventStartDate,
        EventEndDate,
        BusinessTypeId,
        LicenceType,
        ApplicationFee,
        Application_Status_id,
        TownshipId,
        WardNumber,
        RegionName,
        AreaOrSuburb,
        AreaCategory,
        PhysicalAddress,
        PostalAddress,
        Latitude,
        Longitude,
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
        NULLIF(@TradeStandBusinessType, ''),
        @EventStartDate,
        @EventEndDate,
        @BusinessTypeId,
        @LicenceType,
        @ApplicationFee,
        @SubmittedStatusId,
        @TownshipId,
        NULLIF(@WardNumber, ''),
        NULLIF(@RegionName, ''),
        NULL,
        NULLIF(@AreaCategory, ''),
        @PhysicalAddress,
        NULLIF(@PostalAddress, ''),
        @Latitude,
        @Longitude,
        NULLIF(@Notes, '')
    );

    SET @ApplicationId = CONVERT(INT, SCOPE_IDENTITY());
    SET @TrackingNumber = CONCAT('BCLMS-', YEAR(GETDATE()), '-', RIGHT(CONCAT('0000', @ApplicationId), 4));

    UPDATE dbo.Applications
    SET TrackingNumber = @TrackingNumber
    WHERE ApplicationId = @ApplicationId;

    INSERT dbo.ApplicationWorkflowSteps
    (
        ApplicationId,
        StepName,
        AssignedGroupId,
        SequenceNumber,
        Status,
        StartedDate,
        CompletedDate
    )
    SELECT
        @ApplicationId,
        workflow.StepName,
        groups.GroupId,
        workflow.SequenceNumber,
        workflow.Status,
        workflow.StartedDate,
        workflow.CompletedDate
    FROM
    (
        VALUES
            (1, 'Workshop', 'Compliance Officer', 'Approved', GETDATE(), GETDATE(), 0),
            (2, 'Application Submitted', 'Compliance Officer', 'Pending', NULL, NULL, 0),
            (3, 'Home Affairs Verification', 'Compliance Officer', 'Pending', NULL, NULL, 0),
            (4, 'TMPD Inspection (Site Inspection)', @ComplianceGroupName, 'Pending', NULL, NULL, 0),
            (5, 'Admin Approval', 'Admin Officer', 'Pending', NULL, NULL, 0),
            (6, 'Functional Head Approval', 'Functional Head', 'Pending', NULL, NULL, 0),
            (7, 'Director Approval', 'Director', 'Pending', NULL, NULL, 0),
            (8, 'Licence Issued', 'Compliance Officer', 'Pending', NULL, NULL, 0),
            (1, 'Application intake', 'Compliance Officer', 'Pending', NULL, NULL, 1),
            (2, 'Proof of payment verification', 'Compliance Officer', 'Pending', NULL, NULL, 1),
            (3, 'CIPC verification', 'Compliance Officer', 'Pending', NULL, NULL, 1),
            (4, 'Zoning verification', 'City Planning', 'Pending', NULL, NULL, 1),
            (5, 'Health report', 'Health Department', 'Pending', NULL, NULL, 1),
            (6, 'Fire report', 'Fire Department', 'Pending', NULL, NULL, 1),
            (7, 'Senior specialist review', 'Senior Specialist', 'Pending', NULL, NULL, 1),
            (8, 'Admin Approval', 'Admin Officer', 'Pending', NULL, NULL, 1),
            (9, 'Functional Head Approval', 'Functional Head', 'Pending', NULL, NULL, 1),
            (10, 'Director Approval', 'Director', 'Pending', NULL, NULL, 1),
            (11, 'Licence Issued', 'Compliance Officer', 'Pending', NULL, NULL, 1)
    ) AS workflow (SequenceNumber, StepName, GroupName, Status, StartedDate, CompletedDate, FormalOnly)
    INNER JOIN dbo.Groups groups ON groups.GroupName = workflow.GroupName
    WHERE workflow.FormalOnly = @IsFormalBusiness
      AND NOT (@LicenceType LIKE 'Event% Licen%e' AND workflow.StepName = 'TMPD Inspection (Site Inspection)');

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
        applications.TradeStandBusinessType,
        applications.EventStartDate,
        applications.EventEndDate,
        applications.ApplicationFee,
        TownshipId,
        WardNumber,
        RegionName,
        AreaOrSuburb,
        AreaCategory,
        PhysicalAddress,
        PostalAddress,
        Latitude,
        Longitude,
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
UPDATE steps SET Status = 'Approved', CompletedDate = COALESCE(steps.CompletedDate, GETDATE()), Remarks = CONCAT(COALESCE(steps.Remarks + CHAR(13) + CHAR(10), ''), 'Not required for Event Licence; bypassed by workflow rule.')
FROM dbo.ApplicationWorkflowSteps steps JOIN dbo.Applications a ON a.ApplicationId=steps.ApplicationId
WHERE a.LicenceType LIKE 'Event% Licen%e' AND steps.StepName IN ('TMPD Inspection (Site Inspection)', 'Compliance Officer Inspection') AND steps.Status NOT IN ('Approved','Completed','Rejected');
UPDATE a SET CurrentStage = nextStep.StepName
FROM dbo.Applications a CROSS APPLY (SELECT TOP (1) StepName FROM dbo.ApplicationWorkflowSteps s WHERE s.ApplicationId=a.ApplicationId AND s.Status NOT IN ('Approved','Completed') ORDER BY SequenceNumber) nextStep
WHERE a.LicenceType LIKE 'Event% Licen%e' AND a.CurrentStage IN ('TMPD Inspection (Site Inspection)', 'Compliance Officer Inspection');
COMMIT TRANSACTION;
GO
