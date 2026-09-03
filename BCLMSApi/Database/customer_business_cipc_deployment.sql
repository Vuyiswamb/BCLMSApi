SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.CustomerBusinessCipcDocuments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerBusinessCipcDocuments
    (
        CipcDocumentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerBusinessCipcDocuments PRIMARY KEY,
        BusinessId INT NOT NULL,
        DocumentName NVARCHAR(150) NOT NULL CONSTRAINT DF_CustomerBusinessCipcDocuments_DocumentName DEFAULT ('CIPC documents'),
        OriginalFileName NVARCHAR(260) NOT NULL,
        ContentType NVARCHAR(100) NOT NULL,
        FileSizeBytes BIGINT NOT NULL,
        FileContent VARBINARY(MAX) NOT NULL,
        FileSha256Hash NVARCHAR(100) NOT NULL,
        UploadedDate DATETIME2(0) NOT NULL CONSTRAINT DF_CustomerBusinessCipcDocuments_UploadedDate DEFAULT (SYSUTCDATETIME())
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CustomerBusinessCipcDocuments_CustomerBusinesses')
BEGIN
    ALTER TABLE dbo.CustomerBusinessCipcDocuments
    ADD CONSTRAINT FK_CustomerBusinessCipcDocuments_CustomerBusinesses FOREIGN KEY (BusinessId) REFERENCES dbo.CustomerBusinesses(BusinessId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CustomerBusinessCipcDocuments_BusinessId' AND object_id = OBJECT_ID('dbo.CustomerBusinessCipcDocuments'))
BEGIN
    CREATE INDEX IX_CustomerBusinessCipcDocuments_BusinessId ON dbo.CustomerBusinessCipcDocuments(BusinessId, UploadedDate DESC);
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
