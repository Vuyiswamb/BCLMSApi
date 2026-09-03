IF COL_LENGTH('dbo.Users', 'HasAllRegions') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD HasAllRegions BIT NOT NULL CONSTRAINT DF_Users_HasAllRegions DEFAULT (1);
END;
GO

IF OBJECT_ID('dbo.UserRegions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRegions
    (
        UserRegionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserRegions PRIMARY KEY,
        UserId INT NOT NULL,
        RegionName NVARCHAR(120) NOT NULL,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_UserRegions_CreatedDate DEFAULT (GETDATE()),
        CONSTRAINT FK_UserRegions_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT UX_UserRegions_UserId_RegionName UNIQUE (UserId, RegionName)
    );
END;
GO

UPDATE users
SET HasAllRegions = 1
FROM dbo.Users users
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.UserGroups userGroups
    INNER JOIN dbo.Groups groups ON groups.GroupId = userGroups.GroupId
    WHERE userGroups.UserId = users.UserId
      AND groups.GroupName = 'Customer'
);
GO

CREATE OR ALTER PROCEDURE dbo.usp_Auth_GetUserForLogin
    @Username NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        users.UserId,
        users.Username,
        users.DisplayName,
        users.PasswordHash,
        users.PasswordSalt,
        users.PasswordIterations,
        users.IsActive,
        users.HasAllRegions,
        groups.GroupName,
        userRegions.RegionName
    FROM dbo.Users users
    LEFT JOIN dbo.UserGroups userGroups ON userGroups.UserId = users.UserId
    LEFT JOIN dbo.Groups groups ON groups.GroupId = userGroups.GroupId AND groups.IsActive = 1
    LEFT JOIN dbo.UserRegions userRegions ON userRegions.UserId = users.UserId
    WHERE users.Username = @Username
       OR users.EmailAddress = @Username
    ORDER BY groups.GroupName, userRegions.RegionName;
END;
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
      AND
      (
          @CustomerUserId IS NOT NULL
          OR @HasAllRegions = 1
          OR EXISTS
          (
              SELECT 1
              FROM STRING_SPLIT(COALESCE(@RegionNames, ''), ',') regions
              WHERE LTRIM(RTRIM(regions.value)) <> ''
                AND LOWER(LTRIM(RTRIM(regions.value))) = LOWER(LTRIM(RTRIM(COALESCE(applications.RegionName, ''))))
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

    DECLARE @CanView BIT = 0;

    SELECT @CanView = CAST(1 AS BIT)
    FROM dbo.Applications applications
    WHERE applications.ApplicationId = @ApplicationId
      AND applications.Archive_Date IS NULL
      AND (@CustomerUserId IS NULL OR applications.UserId = @CustomerUserId)
      AND
      (
          @CustomerUserId IS NOT NULL
          OR @HasAllRegions = 1
          OR EXISTS
          (
              SELECT 1
              FROM STRING_SPLIT(COALESCE(@RegionNames, ''), ',') regions
              WHERE LTRIM(RTRIM(regions.value)) <> ''
                AND LOWER(LTRIM(RTRIM(regions.value))) = LOWER(LTRIM(RTRIM(COALESCE(applications.RegionName, ''))))
          )
      );

    IF @CanView = 0
    BEGIN
        RETURN;
    END;

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
    WHERE applications.ApplicationId = @ApplicationId;

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
    ORDER BY documents.DocumentName;
END;
GO
