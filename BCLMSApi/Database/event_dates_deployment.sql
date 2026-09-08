SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.Applications', 'EventStartDate') IS NULL ALTER TABLE dbo.Applications ADD EventStartDate DATE NULL;
IF COL_LENGTH('dbo.Applications', 'EventEndDate') IS NULL ALTER TABLE dbo.Applications ADD EventEndDate DATE NULL;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Applications_GetInternal
    @CustomerUserId INT = NULL,
    @HasAllRegions BIT = 1,
    @RegionNames NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        applications.ApplicationId,
        applications.TrackingNumber,
        applications.BusinessId,
        applications.ApplicantName,
        applications.BusinessName,
        COALESCE(businessTypes.BusinessTypeName, applications.LicenceType) AS LicenceType,
        applications.TradeStandBusinessType,
        applications.EventStartDate,
        applications.EventEndDate,
        applications.CurrentStage,
        COALESCE(NULLIF(applications.Status, ''), statuses.StatusName) AS Status,
        applications.SubmittedDate,
        applications.RegionName,
        applications.AreaOrSuburb,
        applications.AreaCategory,
        COUNT(documents.ApplicationDocumentId) AS AttachmentCount,
        CAST(MAX(CASE
            WHEN documents.Status IN ('Submitted', 'Approved')
             AND (
                LOWER(LTRIM(RTRIM(documents.DocumentName))) = 'proof of payment'
                OR LOWER(LTRIM(RTRIM(attachmentTypes.TypeName))) = 'proof of payment'
             )
            THEN 1 ELSE 0 END) AS BIT) AS ProofOfPaymentUploaded
    FROM dbo.Applications applications
    LEFT JOIN dbo.BusinessTypes businessTypes ON businessTypes.BusinessTypeId = applications.BusinessTypeId
    LEFT JOIN dbo.lu_application_status statuses ON statuses.Application_Status_id = applications.Application_Status_id
    LEFT JOIN dbo.ApplicationDocuments documents ON documents.ApplicationId = applications.ApplicationId
    LEFT JOIN dbo.AttachmentTypes attachmentTypes ON attachmentTypes.AttachmentTypeId = documents.AttachmentTypeId
    WHERE applications.Archive_Date IS NULL
      AND (@CustomerUserId IS NULL OR applications.UserId = @CustomerUserId)
      AND (
          @CustomerUserId IS NOT NULL
          OR @HasAllRegions = 1
          OR EXISTS
          (
              SELECT 1
              FROM STRING_SPLIT(COALESCE(@RegionNames, ''), ',') regions
              WHERE LTRIM(RTRIM(regions.value)) = applications.RegionName
          )
      )
    GROUP BY
        applications.ApplicationId,
        applications.TrackingNumber,
        applications.BusinessId,
        applications.ApplicantName,
        applications.BusinessName,
        COALESCE(businessTypes.BusinessTypeName, applications.LicenceType),
        applications.TradeStandBusinessType,
        applications.EventStartDate,
        applications.EventEndDate,
        applications.CurrentStage,
        COALESCE(NULLIF(applications.Status, ''), statuses.StatusName),
        applications.SubmittedDate,
        applications.RegionName,
        applications.AreaOrSuburb,
        applications.AreaCategory
    ORDER BY applications.SubmittedDate DESC;
END;

GO

CREATE OR ALTER PROCEDURE dbo.usp_Applications_GetInternalDetail
    @ApplicationId INT,
    @CustomerUserId INT = NULL,
    @HasAllRegions BIT = 1,
    @RegionNames NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        applications.ApplicationId,
        applications.TrackingNumber,
        applications.ApplicantName,
        applications.IdOrPassportNumber,
        applications.EmailAddress,
        applications.MobileNumber,
        applications.BusinessName,
        applications.BusinessId,
        applications.RegistrationNumber,
        COALESCE(businessTypes.BusinessTypeName, applications.LicenceType) AS LicenceType,
        applications.TradeStandBusinessType,
        applications.EventStartDate,
        applications.EventEndDate,
        applications.ApplicationFee,
        applications.WardNumber,
        applications.RegionName,
        applications.AreaOrSuburb,
        applications.AreaCategory,
        applications.PhysicalAddress,
        applications.PostalAddress,
        applications.Latitude,
        applications.Longitude,
        applications.Notes,
        applications.CurrentStage,
        COALESCE(NULLIF(applications.Status, ''), statuses.StatusName) AS Status,
        applications.SubmittedDate,
        (
            SELECT COUNT(*)
            FROM dbo.ApplicationDocuments documents
            WHERE documents.ApplicationId = applications.ApplicationId
        ) AS AttachmentCount
    FROM dbo.Applications applications
    LEFT JOIN dbo.BusinessTypes businessTypes ON businessTypes.BusinessTypeId = applications.BusinessTypeId
    LEFT JOIN dbo.lu_application_status statuses ON statuses.Application_Status_id = applications.Application_Status_id
    WHERE applications.ApplicationId = @ApplicationId
      AND applications.Archive_Date IS NULL
      AND (@CustomerUserId IS NULL OR applications.UserId = @CustomerUserId)
      AND (
          @CustomerUserId IS NOT NULL
          OR @HasAllRegions = 1
          OR EXISTS
          (
              SELECT 1
              FROM STRING_SPLIT(COALESCE(@RegionNames, ''), ',') regions
              WHERE LTRIM(RTRIM(regions.value)) = applications.RegionName
          )
      );

    SELECT
        steps.StepName,
        groups.GroupName,
        steps.SequenceNumber,
        steps.Status,
        steps.StartedDate,
        steps.CompletedDate,
        steps.Remarks,
        steps.ActionedByUserId,
        steps.ActionedByDisplayName,
        steps.DecisionDate
    FROM dbo.ApplicationWorkflowSteps steps
    INNER JOIN dbo.Groups groups ON groups.GroupId = steps.AssignedGroupId
    WHERE steps.ApplicationId = @ApplicationId
    ORDER BY steps.SequenceNumber;

    SELECT
        documents.ApplicationDocumentId,
        documents.AttachmentTypeId,
        documents.DocumentName,
        documents.IsRequired,
        documents.OriginalFileName,
        documents.ContentType,
        documents.FileSizeBytes,
        documents.SubmittedDate,
        documents.Status,
        documents.Remarks
    FROM dbo.ApplicationDocuments documents
    WHERE documents.ApplicationId = @ApplicationId
    ORDER BY documents.IsRequired DESC, documents.DocumentName;
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
    WHERE workflow.FormalOnly = @IsFormalBusiness;

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

CREATE OR ALTER PROCEDURE dbo.usp_Applications_ResubmitRejectedForCustomer
    @ApplicationId INT,
    @UserId INT,
    @EmailAddress NVARCHAR(150),
    @MobileNumber NVARCHAR(40),
    @PhysicalAddress NVARCHAR(300),
    @PostalAddress NVARCHAR(300) = NULL,
    @TradeStandBusinessType NVARCHAR(120) = NULL,
    @EventStartDate DATE = NULL,
    @EventEndDate DATE = NULL,
    @Latitude DECIMAL(9,6) = NULL,
    @Longitude DECIMAL(9,6) = NULL,
    @Notes NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ResubmittedStatusId INT;

    SELECT @ResubmittedStatusId = Application_Status_id
    FROM dbo.lu_application_status
    WHERE StatusName = 'Resubmitted';

    UPDATE applications
    SET EmailAddress = @EmailAddress,
        MobileNumber = @MobileNumber,
        PhysicalAddress = @PhysicalAddress,
        PostalAddress = NULLIF(@PostalAddress, ''),
        TradeStandBusinessType = NULLIF(@TradeStandBusinessType, ''),
        EventStartDate = @EventStartDate,
        EventEndDate = @EventEndDate,
        Latitude = @Latitude,
        Longitude = @Longitude,
        Notes = NULLIF(@Notes, ''),
        Status = 'Resubmitted',
        Application_Status_id = @ResubmittedStatusId,
        CurrentStage = 'Application intake',
        ModifiedDate = SYSUTCDATETIME()
    FROM dbo.Applications applications
    LEFT JOIN dbo.lu_application_status statuses ON statuses.Application_Status_id = applications.Application_Status_id
    WHERE applications.ApplicationId = @ApplicationId
      AND applications.UserId = @UserId
      AND applications.Archive_Date IS NULL
      AND COALESCE(statuses.StatusName, applications.Status) = 'Rejected';

    IF @@ROWCOUNT = 0
    BEGIN
        RETURN;
    END;

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
        SubmittedDate,
        CAST(CASE WHEN EXISTS
        (
            SELECT 1
            FROM dbo.ApplicationDocuments documents
            LEFT JOIN dbo.AttachmentTypes attachmentTypes ON attachmentTypes.AttachmentTypeId = documents.AttachmentTypeId
            WHERE documents.ApplicationId = applications.ApplicationId
              AND documents.Status IN ('Submitted', 'Approved')
              AND (
                LOWER(LTRIM(RTRIM(documents.DocumentName))) = 'proof of payment'
                OR LOWER(LTRIM(RTRIM(attachmentTypes.TypeName))) = 'proof of payment'
              )
        )
        THEN 1 ELSE 0 END AS BIT) AS ProofOfPaymentUploaded
    FROM dbo.Applications applications
    LEFT JOIN dbo.BusinessTypes businessTypes ON businessTypes.BusinessTypeId = applications.BusinessTypeId
    LEFT JOIN dbo.lu_application_status statuses ON statuses.Application_Status_id = applications.Application_Status_id
    WHERE applications.ApplicationId = @ApplicationId;
END;

GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Applications_EventDateRange')
ALTER TABLE dbo.Applications ADD CONSTRAINT CK_Applications_EventDateRange CHECK (
(EventStartDate IS NULL AND EventEndDate IS NULL) OR
(EventStartDate IS NOT NULL AND EventEndDate IS NOT NULL AND EventEndDate >= EventStartDate));
COMMIT TRANSACTION;
GO
