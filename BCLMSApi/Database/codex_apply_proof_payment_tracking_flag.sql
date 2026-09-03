USE [BCLMS];
GO

CREATE OR ALTER PROCEDURE dbo.usp_Applications_GetInternal
    @CustomerUserId INT = NULL
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

CREATE OR ALTER PROCEDURE dbo.usp_Applications_TrackByTrackingNumber
    @TrackingNumber NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ApplicationId INT;

    SELECT TOP (1)
        @ApplicationId = ApplicationId
    FROM dbo.Applications
    WHERE TrackingNumber = @TrackingNumber
      AND Archive_Date IS NULL;

    SELECT
        ApplicationId,
        TrackingNumber,
        BusinessName,
        COALESCE(businessTypes.BusinessTypeName, applications.LicenceType) AS LicenceType,
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
    WHERE applications.ApplicationId = @ApplicationId
      AND applications.Archive_Date IS NULL;

    SELECT
        aws.ApplicationId,
        aws.StepName,
        g.GroupName,
        aws.Status
    FROM dbo.ApplicationWorkflowSteps aws
    INNER JOIN dbo.Groups g ON g.GroupId = aws.AssignedGroupId
    WHERE aws.ApplicationId = @ApplicationId
    ORDER BY aws.SequenceNumber;
END;
GO
