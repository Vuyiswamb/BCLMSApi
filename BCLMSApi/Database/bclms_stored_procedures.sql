SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
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

CREATE OR ALTER PROCEDURE dbo.usp_Complaints_Create
    @UserId INT,
    @Category NVARCHAR(80),
    @Subject NVARCHAR(180),
    @Description NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CreatedComplaint TABLE (ComplaintId INT NOT NULL);

    INSERT dbo.Complaints
    (
        ReferenceNumber,
        UserId,
        Category,
        Subject,
        Description,
        Status,
        CreatedDate
    )
    OUTPUT INSERTED.ComplaintId INTO @CreatedComplaint(ComplaintId)
    VALUES
    (
        CONCAT('CMP-', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd'), '-', RIGHT(CONCAT('000000', NEXT VALUE FOR dbo.ComplaintReferenceSequence), 6)),
        @UserId,
        @Category,
        @Subject,
        @Description,
        'Submitted',
        SYSUTCDATETIME()
    );

    SELECT
        c.ComplaintId,
        c.ReferenceNumber,
        c.UserId,
        u.DisplayName AS CustomerName,
        ISNULL(u.EmailAddress, u.Username) AS CustomerEmail,
        c.Category,
        c.Subject,
        c.Description,
        c.Status,
        c.OfficialResponse,
        c.CreatedDate,
        c.UpdatedDate
    FROM dbo.Complaints c
    INNER JOIN @CreatedComplaint created ON created.ComplaintId = c.ComplaintId
    INNER JOIN dbo.Users u ON u.UserId = c.UserId;
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
        townships.TOWNSHIP AS TownshipName,
        businesses.WardNumber,
        businesses.PhysicalAddress,
        businesses.Notes,
        cipcDocuments.OriginalFileName AS CipcDocumentFileName,
        CONVERT(BIT, CASE WHEN cipcDocuments.CipcDocumentId IS NULL THEN 0 ELSE 1 END) AS HasCipcDocument,
        businesses.IsActive,
        businesses.CreatedDate
    FROM dbo.CustomerBusinesses businesses
    LEFT JOIN dbo.TOWNSHIPS townships ON townships.ID = businesses.TownshipId
    OUTER APPLY
    (
        SELECT TOP (1) documents.CipcDocumentId, documents.OriginalFileName
        FROM dbo.CustomerBusinessCipcDocuments documents
        WHERE documents.BusinessId = businesses.BusinessId
        ORDER BY documents.UploadedDate DESC, documents.CipcDocumentId DESC
    ) cipcDocuments
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
        townships.TOWNSHIP AS TownshipName,
        businesses.WardNumber,
        businesses.PhysicalAddress,
        businesses.Notes,
        cipcDocuments.OriginalFileName AS CipcDocumentFileName,
        CONVERT(BIT, CASE WHEN cipcDocuments.CipcDocumentId IS NULL THEN 0 ELSE 1 END) AS HasCipcDocument,
        businesses.IsActive,
        businesses.CreatedDate
    FROM dbo.CustomerBusinesses businesses
    LEFT JOIN dbo.TOWNSHIPS townships ON townships.ID = businesses.TownshipId
    OUTER APPLY
    (
        SELECT TOP (1) documents.CipcDocumentId, documents.OriginalFileName
        FROM dbo.CustomerBusinessCipcDocuments documents
        WHERE documents.BusinessId = businesses.BusinessId
        ORDER BY documents.UploadedDate DESC, documents.CipcDocumentId DESC
    ) cipcDocuments
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
    @Notes NVARCHAR(1000) = NULL,
    @CipcDocumentFileName NVARCHAR(260) = NULL,
    @CipcDocumentContentType NVARCHAR(100) = NULL,
    @CipcDocumentFileSizeBytes BIGINT = NULL,
    @CipcDocumentFileContent VARBINARY(MAX) = NULL,
    @CipcDocumentFileSha256Hash NVARCHAR(100) = NULL
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

    IF @CipcDocumentFileContent IS NOT NULL
    BEGIN
        INSERT dbo.CustomerBusinessCipcDocuments
        (
            BusinessId,
            DocumentName,
            OriginalFileName,
            ContentType,
            FileSizeBytes,
            FileContent,
            FileSha256Hash
        )
        SELECT
            created.BusinessId,
            'CIPC documents',
            @CipcDocumentFileName,
            @CipcDocumentContentType,
            @CipcDocumentFileSizeBytes,
            @CipcDocumentFileContent,
            @CipcDocumentFileSha256Hash
        FROM @CreatedBusiness created;
    END;

    SELECT
        businesses.BusinessId,
        businesses.UserId,
        businesses.BusinessName,
        businesses.RegistrationNumber,
        businesses.TownshipId,
        townships.TOWNSHIP AS TownshipName,
        businesses.WardNumber,
        businesses.PhysicalAddress,
        businesses.Notes,
        cipcDocuments.OriginalFileName AS CipcDocumentFileName,
        CONVERT(BIT, CASE WHEN cipcDocuments.CipcDocumentId IS NULL THEN 0 ELSE 1 END) AS HasCipcDocument,
        businesses.IsActive,
        businesses.CreatedDate
    FROM dbo.CustomerBusinesses businesses
    INNER JOIN @CreatedBusiness created ON created.BusinessId = businesses.BusinessId
    LEFT JOIN dbo.TOWNSHIPS townships ON townships.ID = businesses.TownshipId
    OUTER APPLY
    (
        SELECT TOP (1) documents.CipcDocumentId, documents.OriginalFileName
        FROM dbo.CustomerBusinessCipcDocuments documents
        WHERE documents.BusinessId = businesses.BusinessId
        ORDER BY documents.UploadedDate DESC, documents.CipcDocumentId DESC
    ) cipcDocuments;
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
    @Notes NVARCHAR(1000) = NULL,
    @CipcDocumentFileName NVARCHAR(260) = NULL,
    @CipcDocumentContentType NVARCHAR(100) = NULL,
    @CipcDocumentFileSizeBytes BIGINT = NULL,
    @CipcDocumentFileContent VARBINARY(MAX) = NULL,
    @CipcDocumentFileSha256Hash NVARCHAR(100) = NULL
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

    IF @@ROWCOUNT > 0 AND @CipcDocumentFileContent IS NOT NULL
    BEGIN
        INSERT dbo.CustomerBusinessCipcDocuments
        (
            BusinessId,
            DocumentName,
            OriginalFileName,
            ContentType,
            FileSizeBytes,
            FileContent,
            FileSha256Hash
        )
        VALUES
        (
            @BusinessId,
            'CIPC documents',
            @CipcDocumentFileName,
            @CipcDocumentContentType,
            @CipcDocumentFileSizeBytes,
            @CipcDocumentFileContent,
            @CipcDocumentFileSha256Hash
        );
    END;

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

CREATE OR ALTER PROCEDURE dbo.usp_Complaints_GetForCustomer
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.ComplaintId,
        c.ReferenceNumber,
        c.UserId,
        u.DisplayName AS CustomerName,
        ISNULL(u.EmailAddress, u.Username) AS CustomerEmail,
        c.Category,
        c.Subject,
        c.Description,
        c.Status,
        c.OfficialResponse,
        c.CreatedDate,
        c.UpdatedDate
    FROM dbo.Complaints c
    INNER JOIN dbo.Users u ON u.UserId = c.UserId
    WHERE c.UserId = @UserId
    ORDER BY c.CreatedDate DESC, c.ComplaintId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Complaints_GetInternal
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.ComplaintId,
        c.ReferenceNumber,
        c.UserId,
        u.DisplayName AS CustomerName,
        ISNULL(u.EmailAddress, u.Username) AS CustomerEmail,
        c.Category,
        c.Subject,
        c.Description,
        c.Status,
        c.OfficialResponse,
        c.CreatedDate,
        c.UpdatedDate
    FROM dbo.Complaints c
    INNER JOIN dbo.Users u ON u.UserId = c.UserId
    ORDER BY c.CreatedDate DESC, c.ComplaintId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Complaints_UpdateStatus
    @ComplaintId INT,
    @Status NVARCHAR(40),
    @OfficialResponse NVARCHAR(MAX) = NULL,
    @UpdatedByUserId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Complaints
    SET
        Status = @Status,
        OfficialResponse = @OfficialResponse,
        UpdatedDate = SYSUTCDATETIME(),
        UpdatedByUserId = @UpdatedByUserId
    WHERE ComplaintId = @ComplaintId;

    IF @@ROWCOUNT = 0
    BEGIN
        RETURN;
    END;

    SELECT
        c.ComplaintId,
        c.ReferenceNumber,
        c.UserId,
        u.DisplayName AS CustomerName,
        ISNULL(u.EmailAddress, u.Username) AS CustomerEmail,
        c.Category,
        c.Subject,
        c.Description,
        c.Status,
        c.OfficialResponse,
        c.CreatedDate,
        c.UpdatedDate
    FROM dbo.Complaints c
    INNER JOIN dbo.Users u ON u.UserId = c.UserId
    WHERE c.ComplaintId = @ComplaintId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Auth_GetUserByUsernameOrEmail
    @UsernameOrEmail NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        UserId,
        Username,
        DisplayName,
        EmailAddress
    FROM dbo.Users
    WHERE IsActive = 1
      AND (Username = @UsernameOrEmail OR EmailAddress = @UsernameOrEmail);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Auth_UpdateLastLogin
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Users
    SET LastLoginDate = SYSUTCDATETIME(),
        ModifiedDate = SYSUTCDATETIME()
    WHERE UserId = @UserId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Auth_CreatePasswordResetToken
    @UserId INT,
    @TokenHash NVARCHAR(200),
    @HoursToLive INT,
    @RequestedIpAddress NVARCHAR(80) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.PasswordResetTokens
    SET UsedDate = SYSUTCDATETIME()
    WHERE UserId = @UserId
      AND UsedDate IS NULL
      AND ExpiresDate > SYSUTCDATETIME();

    INSERT dbo.PasswordResetTokens (UserId, TokenHash, ExpiresDate, RequestedIpAddress)
    VALUES (@UserId, @TokenHash, DATEADD(HOUR, @HoursToLive, SYSUTCDATETIME()), @RequestedIpAddress);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Auth_GetValidPasswordResetToken
    @TokenHash NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        PasswordResetTokenId,
        UserId
    FROM dbo.PasswordResetTokens
    WHERE TokenHash = @TokenHash
      AND UsedDate IS NULL
      AND ExpiresDate > SYSUTCDATETIME();
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Auth_ResetPassword
    @UserId INT,
    @PasswordHash NVARCHAR(200),
    @PasswordSalt NVARCHAR(100),
    @PasswordIterations INT,
    @TokenHash NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Users
    SET PasswordHash = @PasswordHash,
        PasswordSalt = @PasswordSalt,
        PasswordIterations = @PasswordIterations,
        ModifiedDate = SYSUTCDATETIME()
    WHERE UserId = @UserId;

    UPDATE dbo.PasswordResetTokens
    SET UsedDate = SYSUTCDATETIME()
    WHERE TokenHash = @TokenHash;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Groups_GetActive
    @IncludeCustomer BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        GroupId,
        GroupName,
        Description
    FROM dbo.Groups
    WHERE IsActive = 1
      AND (@IncludeCustomer = 1 OR GroupName <> 'Customer')
    ORDER BY GroupName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Users_GetManaged
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.Username,
        u.DisplayName,
        u.EmailAddress,
        u.IsActive,
        STRING_AGG(g.GroupName, ', ') WITHIN GROUP (ORDER BY g.GroupName) AS Groups
    FROM dbo.Users u
    LEFT JOIN dbo.UserGroups ug ON ug.UserId = u.UserId
    LEFT JOIN dbo.Groups g ON g.GroupId = ug.GroupId
    GROUP BY
        u.UserId,
        u.Username,
        u.DisplayName,
        u.EmailAddress,
        u.IsActive
    ORDER BY u.DisplayName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Users_Create
    @Username NVARCHAR(150),
    @DisplayName NVARCHAR(150),
    @EmailAddress NVARCHAR(150),
    @PasswordHash NVARCHAR(200),
    @PasswordSalt NVARCHAR(100),
    @PasswordIterations INT,
    @GroupIds dbo.IntIdList READONLY
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UserId INT;

    IF EXISTS (SELECT 1 FROM dbo.Users WHERE Username = @Username OR EmailAddress = @EmailAddress)
    BEGIN
        THROW 50002, 'A user with this email address already exists.', 1;
    END;

    INSERT dbo.Users
    (
        Username,
        DisplayName,
        EmailAddress,
        PasswordHash,
        PasswordSalt,
        PasswordIterations,
        IsActive
    )
    VALUES
    (
        @Username,
        @DisplayName,
        @EmailAddress,
        @PasswordHash,
        @PasswordSalt,
        @PasswordIterations,
        1
    );

    SET @UserId = CONVERT(INT, SCOPE_IDENTITY());

    INSERT dbo.UserGroups (UserId, GroupId)
    SELECT @UserId, groupIds.GroupId
    FROM @GroupIds groupIds
    INNER JOIN dbo.Groups groups ON groups.GroupId = groupIds.GroupId AND groups.IsActive = 1;

    SELECT
        u.UserId,
        u.Username,
        u.DisplayName,
        u.EmailAddress,
        u.IsActive,
        STRING_AGG(g.GroupName, ', ') WITHIN GROUP (ORDER BY g.GroupName) AS Groups
    FROM dbo.Users u
    LEFT JOIN dbo.UserGroups ug ON ug.UserId = u.UserId
    LEFT JOIN dbo.Groups g ON g.GroupId = ug.GroupId
    WHERE u.UserId = @UserId
    GROUP BY
        u.UserId,
        u.Username,
        u.DisplayName,
        u.EmailAddress,
        u.IsActive;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_AttachmentTypes_GetActive
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        AttachmentTypeId,
        TypeName,
        Description,
        IsRequired,
        MaxFileSizeBytes,
        AllowedExtension,
        AllowedContentType
    FROM dbo.AttachmentTypes
    WHERE IsActive = 1
    ORDER BY TypeName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Townships_GetActive
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TownshipColumn SYSNAME;
    DECLARE @WardColumn SYSNAME;
    DECLARE @TownshipIdColumn SYSNAME;
    DECLARE @RegionColumn SYSNAME;
    DECLARE @AreaCategoryColumn SYSNAME;
    DECLARE @Sql NVARCHAR(MAX);

    SELECT TOP (1) @TownshipIdColumn = c.name
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID('dbo.TOWNSHIPS')
      AND c.name IN ('TownshipId', 'TownshipID', 'Id', 'ID')
    ORDER BY CASE c.name
        WHEN 'TownshipId' THEN 1
        WHEN 'TownshipID' THEN 2
        WHEN 'Id' THEN 3
        ELSE 4
    END;

    SELECT TOP (1) @TownshipColumn = c.name
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID('dbo.TOWNSHIPS')
      AND c.name IN ('TownshipName', 'Township', 'SuburbName', 'Suburb', 'Name', 'Area', 'AreaOrSuburb')
    ORDER BY CASE c.name
        WHEN 'TownshipName' THEN 1
        WHEN 'Township' THEN 2
        WHEN 'SuburbName' THEN 3
        WHEN 'Suburb' THEN 4
        WHEN 'Name' THEN 5
        WHEN 'Area' THEN 6
        ELSE 7
    END;

    SELECT TOP (1) @WardColumn = c.name
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID('dbo.TOWNSHIPS')
      AND c.name IN ('WardNumber', 'Ward', 'WardNo', 'Ward_No')
    ORDER BY CASE c.name
        WHEN 'WardNumber' THEN 1
        WHEN 'Ward' THEN 2
        WHEN 'WardNo' THEN 3
        ELSE 4
    END;

    SELECT TOP (1) @RegionColumn = c.name
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID('dbo.TOWNSHIPS')
      AND c.name IN ('RegionName', 'Region', 'Region_Name');

    SELECT TOP (1) @AreaCategoryColumn = c.name
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID('dbo.TOWNSHIPS')
      AND c.name IN ('AreaCategory', 'Area_Category', 'RestrictionCategory', 'RestrictedAreaCategory');

    IF @TownshipIdColumn IS NULL OR @TownshipColumn IS NULL OR @WardColumn IS NULL
    BEGIN
        THROW 50001, 'dbo.TOWNSHIPS must include ID, township/suburb, and ward columns.', 1;
    END;

    SET @Sql = N'
        SELECT DISTINCT
            CONVERT(INT, ' + QUOTENAME(@TownshipIdColumn) + N') AS TownshipId,
            CONVERT(NVARCHAR(150), ' + QUOTENAME(@TownshipColumn) + N') AS TownshipName,
            CONVERT(NVARCHAR(20), ' + QUOTENAME(@WardColumn) + N') AS WardNumber,
            ' + CASE WHEN @RegionColumn IS NULL THEN N'CAST(NULL AS NVARCHAR(120))' ELSE N'CONVERT(NVARCHAR(120), ' + QUOTENAME(@RegionColumn) + N')' END + N' AS RegionName,
            ' + CASE WHEN @AreaCategoryColumn IS NULL THEN N'CAST(NULL AS NVARCHAR(80))' ELSE N'CONVERT(NVARCHAR(80), ' + QUOTENAME(@AreaCategoryColumn) + N')' END + N' AS AreaCategory
        FROM dbo.TOWNSHIPS
        WHERE ' + QUOTENAME(@TownshipIdColumn) + N' IS NOT NULL
          AND ' + QUOTENAME(@TownshipColumn) + N' IS NOT NULL
          AND ' + QUOTENAME(@WardColumn) + N' IS NOT NULL
        ORDER BY TownshipName;';

    EXEC sys.sp_executesql @Sql;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Applications_Exists
    @ApplicationId INT,
    @CustomerUserId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CAST(CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.Applications
        WHERE ApplicationId = @ApplicationId
          AND Archive_Date IS NULL
          AND (@CustomerUserId IS NULL OR UserId = @CustomerUserId)
    )
    THEN 1 ELSE 0 END AS BIT) AS ExistsFlag;
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

CREATE OR ALTER PROCEDURE dbo.usp_Applications_ProcessWorkflowStep
    @ApplicationId INT,
    @UserId INT,
    @DisplayName NVARCHAR(150),
    @Decision NVARCHAR(20),
    @Comment NVARCHAR(500) = NULL,
    @RejectionReason NVARCHAR(500) = NULL,
    @AttachmentTypeId INT = NULL,
    @DocumentName NVARCHAR(150) = NULL,
    @OriginalFileName NVARCHAR(260) = NULL,
    @ContentType NVARCHAR(100) = NULL,
    @FileSizeBytes BIGINT = NULL,
    @FileContent VARBINARY(MAX) = NULL,
    @FileSha256Hash NVARCHAR(100) = NULL,
    @SignatureFileSizeBytes BIGINT = NULL,
    @SignatureFileContent VARBINARY(MAX) = NULL,
    @SignatureFileSha256Hash NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StepId INT;
    DECLARE @AssignedGroupId INT;
    DECLARE @SequenceNumber INT;
    DECLARE @StepName NVARCHAR(120);
    DECLARE @NextStepName NVARCHAR(120);
    DECLARE @ApprovedStatusId INT;
    DECLARE @InProgressStatusId INT;
    DECLARE @RejectedStatusId INT;
    DECLARE @CompletedStatusId INT;

    SELECT TOP (1)
        @StepId = steps.ApplicationWorkflowStepId,
        @AssignedGroupId = steps.AssignedGroupId,
        @SequenceNumber = steps.SequenceNumber,
        @StepName = steps.StepName
    FROM dbo.ApplicationWorkflowSteps steps
    INNER JOIN dbo.Applications applications ON applications.ApplicationId = steps.ApplicationId
    WHERE steps.ApplicationId = @ApplicationId
      AND applications.Archive_Date IS NULL
      AND steps.Status IN ('Pending', 'In progress', 'Submitted', 'Under Review')
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.ApplicationWorkflowSteps previousSteps
          WHERE previousSteps.ApplicationId = steps.ApplicationId
            AND previousSteps.SequenceNumber < steps.SequenceNumber
            AND previousSteps.Status NOT IN ('Approved', 'Completed', 'Complete')
      )
    ORDER BY steps.SequenceNumber;

    IF @StepId IS NULL
    BEGIN
        THROW 50000, 'There is no active workflow step to process.', 1;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.UserGroups userGroups
        WHERE userGroups.UserId = @UserId
          AND userGroups.GroupId = @AssignedGroupId
    )
    AND NOT EXISTS
    (
        SELECT 1
        FROM dbo.UserGroups userGroups
        INNER JOIN dbo.Groups groups ON groups.GroupId = userGroups.GroupId
        WHERE userGroups.UserId = @UserId
          AND groups.GroupName = 'Super User'
    )
    BEGIN
        THROW 50001, 'You are not assigned to process this workflow step.', 1;
    END;

    IF @Decision NOT IN ('Approve', 'Reject', 'Complete')
    BEGIN
        THROW 50002, 'Select Approve, Reject, or Complete.', 1;
    END;

    IF @Decision = 'Complete' AND @StepName <> 'Licence Issued'
    BEGIN
        THROW 50004, 'Only the Licence Issued step can complete the application.', 1;
    END;

    IF @Decision = 'Reject' AND NULLIF(LTRIM(RTRIM(@RejectionReason)), '') IS NULL
    BEGIN
        THROW 50003, 'A rejection reason is required.', 1;
    END;

    SELECT @ApprovedStatusId = Application_Status_id
    FROM dbo.lu_application_status
    WHERE StatusName = 'Approved';

    SELECT @InProgressStatusId = Application_Status_id
    FROM dbo.lu_application_status
    WHERE StatusName = 'In progress';

    SELECT @RejectedStatusId = Application_Status_id
    FROM dbo.lu_application_status
    WHERE StatusName = 'Rejected';

    SELECT @CompletedStatusId = Application_Status_id
    FROM dbo.lu_application_status
    WHERE StatusName = 'Completed';

    IF @Decision = 'Approve'
    BEGIN
        SELECT TOP (1) @NextStepName = StepName
        FROM dbo.ApplicationWorkflowSteps
        WHERE ApplicationId = @ApplicationId
          AND SequenceNumber > @SequenceNumber
        ORDER BY SequenceNumber;

        IF @NextStepName = 'Proof of payment verification'
           AND NOT EXISTS
           (
               SELECT 1
               FROM dbo.ApplicationDocuments documents
               LEFT JOIN dbo.AttachmentTypes attachmentTypes ON attachmentTypes.AttachmentTypeId = documents.AttachmentTypeId
               WHERE documents.ApplicationId = @ApplicationId
                 AND documents.Status IN ('Submitted', 'Approved')
                 AND (
                     LOWER(LTRIM(RTRIM(documents.DocumentName))) = 'proof of payment'
                     OR LOWER(LTRIM(RTRIM(attachmentTypes.TypeName))) = 'proof of payment'
                 )
           )
        BEGIN
            THROW 50005, 'Proof of payment must be uploaded before the application can move to Proof of payment verification.', 1;
        END;
    END;

    IF @FileContent IS NOT NULL
    BEGIN
        INSERT dbo.ApplicationDocuments
        (
            ApplicationId,
            AttachmentTypeId,
            DocumentName,
            IsRequired,
            OriginalFileName,
            StoredFileName,
            ContentType,
            FileSizeBytes,
            FileContent,
            FileSha256Hash,
            SubmittedDate,
            Status,
            Remarks
        )
        VALUES
        (
            @ApplicationId,
            @AttachmentTypeId,
            COALESCE(NULLIF(@DocumentName, ''), CONCAT(@StepName, ' document')),
            0,
            @OriginalFileName,
            CONCAT(REPLACE(CONVERT(NVARCHAR(36), NEWID()), '-', ''), '.pdf'),
            @ContentType,
            @FileSizeBytes,
            @FileContent,
            @FileSha256Hash,
            SYSUTCDATETIME(),
            'Submitted',
            NULLIF(@Comment, '')
        );
    END;

    IF @SignatureFileContent IS NOT NULL
    BEGIN
        INSERT dbo.ApplicationDocuments
        (
            ApplicationId,
            AttachmentTypeId,
            DocumentName,
            IsRequired,
            OriginalFileName,
            StoredFileName,
            ContentType,
            FileSizeBytes,
            FileContent,
            FileSha256Hash,
            SubmittedDate,
            Status,
            Remarks
        )
        VALUES
        (
            @ApplicationId,
            NULL,
            CONCAT(@StepName, ' signature'),
            0,
            'admin-approval-signature.png',
            CONCAT(REPLACE(CONVERT(NVARCHAR(36), NEWID()), '-', ''), '.png'),
            'image/png',
            @SignatureFileSizeBytes,
            @SignatureFileContent,
            @SignatureFileSha256Hash,
            SYSUTCDATETIME(),
            'Submitted',
            'Official signature captured during workflow approval'
        );
    END;

    IF @Decision = 'Reject'
    BEGIN
        UPDATE dbo.ApplicationWorkflowSteps
        SET Status = 'Rejected',
            StartedDate = COALESCE(StartedDate, SYSUTCDATETIME()),
            CompletedDate = SYSUTCDATETIME(),
            Remarks = COALESCE(NULLIF(@RejectionReason, ''), NULLIF(@Comment, '')),
            ActionedByUserId = @UserId,
            ActionedByDisplayName = @DisplayName,
            DecisionDate = SYSUTCDATETIME()
        WHERE ApplicationWorkflowStepId = @StepId;

        UPDATE dbo.Applications
        SET Status = 'Rejected',
            Application_Status_id = @RejectedStatusId,
            CurrentStage = @StepName,
            ModifiedDate = SYSUTCDATETIME()
        WHERE ApplicationId = @ApplicationId;
    END;
    ELSE IF @Decision = 'Complete'
    BEGIN
        UPDATE dbo.ApplicationWorkflowSteps
        SET Status = 'Completed',
            StartedDate = COALESCE(StartedDate, SYSUTCDATETIME()),
            CompletedDate = SYSUTCDATETIME(),
            Remarks = COALESCE(NULLIF(@Comment, ''), 'Application completed after licence issue.'),
            ActionedByUserId = @UserId,
            ActionedByDisplayName = @DisplayName,
            DecisionDate = SYSUTCDATETIME()
        WHERE ApplicationWorkflowStepId = @StepId;

        UPDATE dbo.Applications
        SET Status = 'Completed',
            Application_Status_id = COALESCE(@CompletedStatusId, @ApprovedStatusId),
            CurrentStage = 'Completed',
            ModifiedDate = SYSUTCDATETIME()
        WHERE ApplicationId = @ApplicationId;
    END;
    ELSE
    BEGIN
        UPDATE dbo.ApplicationWorkflowSteps
        SET Status = 'Approved',
            StartedDate = COALESCE(StartedDate, SYSUTCDATETIME()),
            CompletedDate = SYSUTCDATETIME(),
            Remarks = NULLIF(@Comment, ''),
            ActionedByUserId = @UserId,
            ActionedByDisplayName = @DisplayName,
            DecisionDate = SYSUTCDATETIME()
        WHERE ApplicationWorkflowStepId = @StepId;

        IF @NextStepName IS NULL
        BEGIN
            UPDATE dbo.Applications
            SET Status = 'Approved',
                Application_Status_id = @ApprovedStatusId,
                CurrentStage = 'Completed',
                ModifiedDate = SYSUTCDATETIME()
            WHERE ApplicationId = @ApplicationId;
        END;
        ELSE
        BEGIN
            UPDATE dbo.ApplicationWorkflowSteps
            SET Status = 'In progress',
                StartedDate = COALESCE(StartedDate, SYSUTCDATETIME())
            WHERE ApplicationId = @ApplicationId
              AND SequenceNumber = @SequenceNumber + 1
              AND Status = 'Pending';

            UPDATE dbo.Applications
            SET Status = 'In progress',
                Application_Status_id = @InProgressStatusId,
                CurrentStage = @NextStepName,
                ModifiedDate = SYSUTCDATETIME()
            WHERE ApplicationId = @ApplicationId;
        END;
    END;

    SELECT @ApplicationId AS ApplicationId;
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

CREATE OR ALTER PROCEDURE dbo.usp_Applications_Archive
    @ApplicationId INT,
    @ArchiveUserId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Applications
    SET Archive_Date = SYSUTCDATETIME(),
        archive_user_id = @ArchiveUserId,
        ModifiedDate = SYSUTCDATETIME()
    WHERE ApplicationId = @ApplicationId
      AND Archive_Date IS NULL;

    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Applications_CancelSubmittedForCustomer
    @ApplicationId INT,
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Applications
    SET Archive_Date = SYSUTCDATETIME(),
        archive_user_id = @UserId,
        ModifiedDate = SYSUTCDATETIME()
    WHERE ApplicationId = @ApplicationId
      AND UserId = @UserId
      AND Archive_Date IS NULL
      AND Status = 'Submitted';

    SELECT @@ROWCOUNT AS RowsAffected;
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

CREATE OR ALTER PROCEDURE dbo.usp_Applications_NewApplicationExistsForApplicant
    @IdOrPassportNumber NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CAST(0 AS BIT) AS ExistsFlag;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Applications_BusinessExistsForApplicant
    @IdOrPassportNumber NVARCHAR(50),
    @BusinessName NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CAST(CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.Applications applications
        WHERE LOWER(LTRIM(RTRIM(applications.IdOrPassportNumber))) = LOWER(LTRIM(RTRIM(@IdOrPassportNumber)))
          AND LOWER(LTRIM(RTRIM(applications.BusinessName))) = LOWER(LTRIM(RTRIM(@BusinessName)))
          AND applications.Archive_Date IS NULL
    )
    THEN 1 ELSE 0 END AS BIT) AS ExistsFlag;
END;
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
        SubmittedDate
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

CREATE OR ALTER PROCEDURE dbo.usp_AttachmentTypes_GetById
    @AttachmentTypeId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        AttachmentTypeId,
        TypeName,
        IsRequired
    FROM dbo.AttachmentTypes
    WHERE AttachmentTypeId = @AttachmentTypeId
      AND IsActive = 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ApplicationDocuments_InsertPdf
    @ApplicationId INT,
    @AttachmentTypeId INT,
    @DocumentName NVARCHAR(150),
    @IsRequired BIT,
    @OriginalFileName NVARCHAR(260),
    @StoredFileName NVARCHAR(260),
    @ContentType NVARCHAR(100),
    @FileSizeBytes BIGINT,
    @FileContent VARBINARY(MAX),
    @FileSha256Hash NVARCHAR(100),
    @Remarks NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.ApplicationDocuments
    (
        ApplicationId,
        AttachmentTypeId,
        DocumentName,
        IsRequired,
        OriginalFileName,
        StoredFileName,
        ContentType,
        FileSizeBytes,
        FileContent,
        FileSha256Hash,
        SubmittedDate,
        Status,
        Remarks
    )
    OUTPUT INSERTED.ApplicationDocumentId
    VALUES
    (
        @ApplicationId,
        @AttachmentTypeId,
        @DocumentName,
        @IsRequired,
        @OriginalFileName,
        @StoredFileName,
        @ContentType,
        @FileSizeBytes,
        @FileContent,
        @FileSha256Hash,
        SYSUTCDATETIME(),
        'Submitted',
        @Remarks
    );
END;
GO
