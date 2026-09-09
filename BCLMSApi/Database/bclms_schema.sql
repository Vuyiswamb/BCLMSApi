SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF TYPE_ID('dbo.IntIdList') IS NULL
BEGIN
    CREATE TYPE dbo.IntIdList AS TABLE
    (
        GroupId INT NOT NULL
    );
END;
GO

IF OBJECT_ID('dbo.ApplicationDocuments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApplicationDocuments
    (
        ApplicationDocumentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApplicationDocuments PRIMARY KEY,
        ApplicationId INT NOT NULL,
        AttachmentTypeId INT NULL,
        DocumentName NVARCHAR(150) NOT NULL,
        IsRequired BIT NOT NULL CONSTRAINT DF_ApplicationDocuments_IsRequired DEFAULT (1),
        OriginalFileName NVARCHAR(260) NULL,
        StoredFileName NVARCHAR(260) NULL,
        ContentType NVARCHAR(100) NULL,
        FileSizeBytes BIGINT NULL,
        FileContent VARBINARY(MAX) NULL,
        FileSha256Hash NVARCHAR(100) NULL,
        SubmittedDate DATETIME2(0) NULL,
        Status NVARCHAR(40) NOT NULL CONSTRAINT DF_ApplicationDocuments_Status DEFAULT ('Not submitted'),
        Remarks NVARCHAR(500) NULL,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_ApplicationDocuments_CreatedDate DEFAULT (SYSUTCDATETIME())
    );
END;
GO

IF OBJECT_ID('dbo.AttachmentTypes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AttachmentTypes
    (
        AttachmentTypeId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AttachmentTypes PRIMARY KEY,
        TypeName NVARCHAR(150) NOT NULL CONSTRAINT UQ_AttachmentTypes_TypeName UNIQUE,
        Description NVARCHAR(300) NULL,
        IsRequired BIT NOT NULL CONSTRAINT DF_AttachmentTypes_IsRequired DEFAULT (1),
        MaxFileSizeBytes BIGINT NOT NULL CONSTRAINT DF_AttachmentTypes_MaxFileSizeBytes DEFAULT (10485760),
        AllowedExtension NVARCHAR(10) NOT NULL CONSTRAINT DF_AttachmentTypes_AllowedExtension DEFAULT ('.pdf'),
        AllowedContentType NVARCHAR(100) NOT NULL CONSTRAINT DF_AttachmentTypes_AllowedContentType DEFAULT ('application/pdf'),
        IsActive BIT NOT NULL CONSTRAINT DF_AttachmentTypes_IsActive DEFAULT (1),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_AttachmentTypes_CreatedDate DEFAULT (SYSUTCDATETIME())
    );
END;
GO

IF OBJECT_ID('dbo.ApplicationWorkflowSteps', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApplicationWorkflowSteps
    (
        ApplicationWorkflowStepId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApplicationWorkflowSteps PRIMARY KEY,
        ApplicationId INT NOT NULL,
        StepName NVARCHAR(120) NOT NULL,
        AssignedGroupId INT NOT NULL,
        SequenceNumber INT NOT NULL,
        Status NVARCHAR(40) NOT NULL CONSTRAINT DF_ApplicationWorkflowSteps_Status DEFAULT ('Pending'),
        StartedDate DATETIME2(0) NULL,
        CompletedDate DATETIME2(0) NULL,
        Remarks NVARCHAR(500) NULL,
        ActionedByUserId INT NULL,
        ActionedByDisplayName NVARCHAR(150) NULL,
        DecisionDate DATETIME2(0) NULL,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_ApplicationWorkflowSteps_CreatedDate DEFAULT (SYSUTCDATETIME())
    );
END;
GO

IF COL_LENGTH('dbo.ApplicationWorkflowSteps', 'ActionedByUserId') IS NULL
BEGIN
    ALTER TABLE dbo.ApplicationWorkflowSteps ADD ActionedByUserId INT NULL;
END;
GO

IF COL_LENGTH('dbo.ApplicationWorkflowSteps', 'ActionedByDisplayName') IS NULL
BEGIN
    ALTER TABLE dbo.ApplicationWorkflowSteps ADD ActionedByDisplayName NVARCHAR(150) NULL;
END;
GO

IF COL_LENGTH('dbo.ApplicationWorkflowSteps', 'DecisionDate') IS NULL
BEGIN
    ALTER TABLE dbo.ApplicationWorkflowSteps ADD DecisionDate DATETIME2(0) NULL;
END;
GO

IF OBJECT_ID('dbo.Applications', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Applications
    (
        ApplicationId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Applications PRIMARY KEY,
        TrackingNumber NVARCHAR(30) NOT NULL CONSTRAINT UQ_Applications_TrackingNumber UNIQUE,
        ApplicantName NVARCHAR(150) NOT NULL,
        IdOrPassportNumber NVARCHAR(50) NOT NULL,
        EmailAddress NVARCHAR(150) NOT NULL,
        MobileNumber NVARCHAR(40) NOT NULL,
        BusinessName NVARCHAR(200) NOT NULL,
        RegistrationNumber NVARCHAR(80) NULL,
        LicenceType NVARCHAR(100) NOT NULL,
        ApplicationFee DECIMAL(18,2) NULL,
        TownshipId INT NULL,
        WardNumber NVARCHAR(20) NULL,
        RegionName NVARCHAR(120) NULL,
        AreaCategory NVARCHAR(80) NULL,
        AreaOrSuburb NVARCHAR(120) NULL,
        PhysicalAddress NVARCHAR(300) NOT NULL,
        PostalAddress NVARCHAR(300) NULL,
        Latitude DECIMAL(9,6) NULL,
        Longitude DECIMAL(9,6) NULL,
        Notes NVARCHAR(1000) NULL,
        CurrentStage NVARCHAR(120) NOT NULL CONSTRAINT DF_Applications_CurrentStage DEFAULT ('Application intake'),
        Status NVARCHAR(40) NOT NULL CONSTRAINT DF_Applications_Status DEFAULT ('Submitted'),
        SubmittedDate DATETIME2(0) NOT NULL CONSTRAINT DF_Applications_SubmittedDate DEFAULT (GETDATE()),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_Applications_CreatedDate DEFAULT (GETDATE()),
        Archive_Date DATETIME2(0) NULL,
        archive_user_id INT NULL,
        ModifiedDate DATETIME2(0) NULL
    );
END;
GO

IF OBJECT_ID('dbo.BusinessTypes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BusinessTypes
    (
        BusinessTypeId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BusinessTypes PRIMARY KEY,
        BusinessTypeName NVARCHAR(100) NOT NULL CONSTRAINT UQ_BusinessTypes_BusinessTypeName UNIQUE,
        Description NVARCHAR(250) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_BusinessTypes_IsActive DEFAULT (1),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_BusinessTypes_CreatedDate DEFAULT (SYSUTCDATETIME())
    );
END;
GO

IF OBJECT_ID('dbo.Tariffs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tariffs
    (
        TariffId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Tariffs PRIMARY KEY,
        LicenceType NVARCHAR(100) NOT NULL,
        ApplicationKind NVARCHAR(40) NOT NULL,
        FeeAmount DECIMAL(18,2) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Tariffs_IsActive DEFAULT (1),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_Tariffs_CreatedDate DEFAULT (SYSUTCDATETIME()),
        ModifiedDate DATETIME2(0) NULL
    );
END;
GO

IF OBJECT_ID('dbo.lu_application_status', 'U') IS NULL AND OBJECT_ID('dbo.lookup_status', 'U') IS NOT NULL
BEGIN
    EXEC sp_rename 'dbo.lookup_status', 'lu_application_status';
END;
GO

IF OBJECT_ID('dbo.lu_application_status', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.lu_application_status
    (
        Application_Status_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_lu_application_status PRIMARY KEY,
        StatusName NVARCHAR(40) NOT NULL CONSTRAINT UQ_lu_application_status_StatusName UNIQUE,
        Description NVARCHAR(250) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_lu_application_status_IsActive DEFAULT (1),
        SortOrder INT NOT NULL CONSTRAINT DF_lu_application_status_SortOrder DEFAULT (0),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_lu_application_status_CreatedDate DEFAULT (SYSUTCDATETIME())
    );
END;
GO

IF COL_LENGTH('dbo.Applications', 'TownshipId') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD TownshipId INT NULL;
END;
GO

IF COL_LENGTH('dbo.Applications', 'BusinessTypeId') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD BusinessTypeId INT NULL;
END;
GO

IF COL_LENGTH('dbo.Applications', 'Application_Status_id') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD Application_Status_id INT NULL;
END;
GO

IF COL_LENGTH('dbo.Applications', 'UserId') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD UserId INT NULL;
END;
GO

IF COL_LENGTH('dbo.Applications', 'BusinessId') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD BusinessId INT NULL;
END;
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

IF COL_LENGTH('dbo.Applications', 'TradeStandBusinessType') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD TradeStandBusinessType NVARCHAR(120) NULL;
END;
GO

IF COL_LENGTH('dbo.Applications', 'PrePackedPerishableGoods') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD PrePackedPerishableGoods NVARCHAR(1000) NULL;
END;
GO

IF COL_LENGTH('dbo.Applications', 'RegionName') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD RegionName NVARCHAR(120) NULL;
END;
GO

IF COL_LENGTH('dbo.Applications', 'AreaCategory') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD AreaCategory NVARCHAR(80) NULL;
END;
GO

IF COL_LENGTH('dbo.Applications', 'Archive_Date') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD Archive_Date DATETIME2(0) NULL;
END;
GO

IF COL_LENGTH('dbo.Applications', 'archive_user_id') IS NULL
BEGIN
    ALTER TABLE dbo.Applications ADD archive_user_id INT NULL;
END;
GO

IF COL_LENGTH('dbo.Applications', 'AreaOrSuburb') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Applications ALTER COLUMN AreaOrSuburb NVARCHAR(120) NULL;
END;
GO

IF OBJECT_ID('dbo.Groups', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Groups
    (
        GroupId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Groups PRIMARY KEY,
        GroupName NVARCHAR(80) NOT NULL CONSTRAINT UQ_Groups_GroupName UNIQUE,
        Description NVARCHAR(250) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Groups_IsActive DEFAULT (1),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_Groups_CreatedDate DEFAULT (SYSUTCDATETIME())
    );
END;
GO

IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        UserId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        Username NVARCHAR(80) NOT NULL CONSTRAINT UQ_Users_Username UNIQUE,
        DisplayName NVARCHAR(150) NOT NULL,
        EmailAddress NVARCHAR(150) NULL,
        PasswordHash NVARCHAR(200) NOT NULL,
        PasswordSalt NVARCHAR(100) NOT NULL,
        PasswordIterations INT NOT NULL,
        HasAllRegions BIT NOT NULL CONSTRAINT DF_Users_HasAllRegions DEFAULT (1),
        IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
        LastLoginDate DATETIME2(0) NULL,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_Users_CreatedDate DEFAULT (SYSUTCDATETIME()),
        ModifiedDate DATETIME2(0) NULL
    );
END;
GO

IF COL_LENGTH('dbo.Users', 'HasAllRegions') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD HasAllRegions BIT NOT NULL CONSTRAINT DF_Users_HasAllRegions DEFAULT (1);
END;
GO

IF OBJECT_ID('dbo.UserGroups', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserGroups
    (
        UserGroupId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserGroups PRIMARY KEY,
        UserId INT NOT NULL,
        GroupId INT NOT NULL,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_UserGroups_CreatedDate DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_UserGroups_UserId_GroupId UNIQUE (UserId, GroupId)
    );
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

IF OBJECT_ID('dbo.PasswordResetTokens', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetTokens
    (
        PasswordResetTokenId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PasswordResetTokens PRIMARY KEY,
        UserId INT NOT NULL,
        TokenHash NVARCHAR(200) NOT NULL CONSTRAINT UQ_PasswordResetTokens_TokenHash UNIQUE,
        ExpiresDate DATETIME2(0) NOT NULL,
        UsedDate DATETIME2(0) NULL,
        RequestedIpAddress NVARCHAR(80) NULL,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_PasswordResetTokens_CreatedDate DEFAULT (SYSUTCDATETIME())
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_UserGroups_Users')
BEGIN
    ALTER TABLE dbo.UserGroups
    ADD CONSTRAINT FK_UserGroups_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_UserGroups_Groups')
BEGIN
    ALTER TABLE dbo.UserGroups
    ADD CONSTRAINT FK_UserGroups_Groups FOREIGN KEY (GroupId) REFERENCES dbo.Groups(GroupId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ApplicationWorkflowSteps_Applications')
BEGIN
    ALTER TABLE dbo.ApplicationWorkflowSteps
    ADD CONSTRAINT FK_ApplicationWorkflowSteps_Applications FOREIGN KEY (ApplicationId) REFERENCES dbo.Applications(ApplicationId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ApplicationWorkflowSteps_Groups')
BEGIN
    ALTER TABLE dbo.ApplicationWorkflowSteps
    ADD CONSTRAINT FK_ApplicationWorkflowSteps_Groups FOREIGN KEY (AssignedGroupId) REFERENCES dbo.Groups(GroupId);
END;
GO

IF OBJECT_ID('dbo.Complaints', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Complaints
    (
        ComplaintId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Complaints PRIMARY KEY,
        ReferenceNumber NVARCHAR(30) NOT NULL CONSTRAINT UQ_Complaints_ReferenceNumber UNIQUE,
        UserId INT NOT NULL,
        Category NVARCHAR(80) NOT NULL,
        Subject NVARCHAR(180) NOT NULL,
        Description NVARCHAR(MAX) NOT NULL,
        Status NVARCHAR(40) NOT NULL CONSTRAINT DF_Complaints_Status DEFAULT ('Submitted'),
        OfficialResponse NVARCHAR(MAX) NULL,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_Complaints_CreatedDate DEFAULT (SYSUTCDATETIME()),
        UpdatedDate DATETIME2(0) NULL,
        UpdatedByUserId INT NULL
    );
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
        BusinessPhotoContent VARBINARY(MAX) NULL,
        BusinessPhotoContentType NVARCHAR(100) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_CustomerBusinesses_IsActive DEFAULT (1),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_CustomerBusinesses_CreatedDate DEFAULT (SYSUTCDATETIME()),
        ModifiedDate DATETIME2(0) NULL
    );
END;
GO

IF COL_LENGTH('dbo.CustomerBusinesses', 'BusinessPhotoContent') IS NULL
BEGIN
    ALTER TABLE dbo.CustomerBusinesses ADD BusinessPhotoContent VARBINARY(MAX) NULL;
END;
GO

IF COL_LENGTH('dbo.CustomerBusinesses', 'BusinessPhotoContentType') IS NULL
BEGIN
    ALTER TABLE dbo.CustomerBusinesses ADD BusinessPhotoContentType NVARCHAR(100) NULL;
END;
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

IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = 'ComplaintReferenceSequence' AND SCHEMA_NAME(schema_id) = 'dbo')
BEGIN
    CREATE SEQUENCE dbo.ComplaintReferenceSequence
        AS INT
        START WITH 1
        INCREMENT BY 1;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Complaints_Users')
BEGIN
    ALTER TABLE dbo.Complaints
    ADD CONSTRAINT FK_Complaints_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Complaints_UpdatedByUsers')
BEGIN
    ALTER TABLE dbo.Complaints
    ADD CONSTRAINT FK_Complaints_UpdatedByUsers FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.Users(UserId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CustomerBusinesses_Users')
BEGIN
    ALTER TABLE dbo.CustomerBusinesses
    ADD CONSTRAINT FK_CustomerBusinesses_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CustomerBusinesses_Townships')
   AND COL_LENGTH('dbo.CustomerBusinesses', 'TownshipId') IS NOT NULL
   AND COL_LENGTH('dbo.Townships', 'TownshipId') IS NOT NULL
BEGIN
    ALTER TABLE dbo.CustomerBusinesses
    ADD CONSTRAINT FK_CustomerBusinesses_Townships FOREIGN KEY (TownshipId) REFERENCES dbo.Townships(TownshipId);
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

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ApplicationDocuments_Applications')
BEGIN
    ALTER TABLE dbo.ApplicationDocuments
    ADD CONSTRAINT FK_ApplicationDocuments_Applications FOREIGN KEY (ApplicationId) REFERENCES dbo.Applications(ApplicationId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ApplicationDocuments_AttachmentTypes')
BEGIN
    ALTER TABLE dbo.ApplicationDocuments
    ADD CONSTRAINT FK_ApplicationDocuments_AttachmentTypes FOREIGN KEY (AttachmentTypeId) REFERENCES dbo.AttachmentTypes(AttachmentTypeId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PasswordResetTokens_Users')
BEGIN
    ALTER TABLE dbo.PasswordResetTokens
    ADD CONSTRAINT FK_PasswordResetTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Applications_BusinessTypes')
BEGIN
    ALTER TABLE dbo.Applications
    ADD CONSTRAINT FK_Applications_BusinessTypes FOREIGN KEY (BusinessTypeId) REFERENCES dbo.BusinessTypes(BusinessTypeId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name IN ('FK_Applications_lu_application_status', 'FK_Applications_lookup_status'))
BEGIN
    ALTER TABLE dbo.Applications
    ADD CONSTRAINT FK_Applications_lu_application_status FOREIGN KEY (Application_Status_id) REFERENCES dbo.lu_application_status(Application_Status_id);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Applications_Users')
BEGIN
    ALTER TABLE dbo.Applications
    ADD CONSTRAINT FK_Applications_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Applications_CustomerBusinesses')
BEGIN
    ALTER TABLE dbo.Applications
    ADD CONSTRAINT FK_Applications_CustomerBusinesses FOREIGN KEY (BusinessId) REFERENCES dbo.CustomerBusinesses(BusinessId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Applications_BusinessName' AND object_id = OBJECT_ID('dbo.Applications'))
BEGIN
    CREATE INDEX IX_Applications_BusinessName ON dbo.Applications(BusinessName);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Tariffs_LicenceType_ApplicationKind' AND object_id = OBJECT_ID('dbo.Tariffs'))
BEGIN
    CREATE UNIQUE INDEX UX_Tariffs_LicenceType_ApplicationKind ON dbo.Tariffs(LicenceType, ApplicationKind);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Applications_BusinessTypeId' AND object_id = OBJECT_ID('dbo.Applications'))
BEGIN
    CREATE INDEX IX_Applications_BusinessTypeId ON dbo.Applications(BusinessTypeId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Applications_Application_Status_id' AND object_id = OBJECT_ID('dbo.Applications'))
BEGIN
    CREATE INDEX IX_Applications_Application_Status_id ON dbo.Applications(Application_Status_id);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Applications_UserId' AND object_id = OBJECT_ID('dbo.Applications'))
BEGIN
    CREATE INDEX IX_Applications_UserId ON dbo.Applications(UserId);
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Applications_Archive_Date' AND object_id = OBJECT_ID('dbo.Applications'))
BEGIN
    CREATE INDEX IX_Applications_Archive_Date ON dbo.Applications(Archive_Date);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ApplicationWorkflowSteps_AssignedGroupId_Status' AND object_id = OBJECT_ID('dbo.ApplicationWorkflowSteps'))
BEGIN
    CREATE INDEX IX_ApplicationWorkflowSteps_AssignedGroupId_Status ON dbo.ApplicationWorkflowSteps(AssignedGroupId, Status);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PasswordResetTokens_UserId_ExpiresDate' AND object_id = OBJECT_ID('dbo.PasswordResetTokens'))
BEGIN
    CREATE INDEX IX_PasswordResetTokens_UserId_ExpiresDate ON dbo.PasswordResetTokens(UserId, ExpiresDate);
END;
GO

MERGE dbo.AttachmentTypes AS target
USING
(
    VALUES
        ('Zoning certificate', 'Municipal zoning certificate'),
        ('Approved building plans', 'Approved building plans for premises'),
        ('ID / passport', 'Applicant identity document or passport'),
        ('CIPC documents', 'Company registration documents'),
        ('Power of attorney', 'Authority letter if applying on behalf of someone'),
        ('Lease agreement', 'Signed lease agreement'),
        ('Title deed document', 'Title deed where applicable'),
        ('Proof of residence', 'Applicant proof of residence'),
        ('SARS documents', 'Tax registration or compliance documents'),
        ('Affidavit if Res 5', 'Affidavit required for Res 5 cases'),
        ('Menu for cafe keeper', 'Menu for cafe keeper applications'),
        ('Health report', 'Health department report'),
        ('Fire report', 'Fire department report'),
        ('Liquor licence', 'Liquor licence where applicable'),
        ('Gambling authority', 'Gambling authority approval where applicable'),
        ('Proof of payment', 'Proof of application payment'),
        ('COA (Temporary Certificate of Acceptance)', 'Temporary Certificate of Acceptance for event licence applications')
) AS source (TypeName, Description)
ON target.TypeName = source.TypeName
WHEN MATCHED THEN
    UPDATE SET Description = source.Description, IsActive = 1
WHEN NOT MATCHED THEN
    INSERT (TypeName, Description) VALUES (source.TypeName, source.Description);
GO

UPDATE dbo.AttachmentTypes
SET IsRequired = 0
WHERE TypeName = 'Proof of payment';
GO

UPDATE dbo.ApplicationWorkflowSteps
SET StepName = 'CIPC verification'
WHERE StepName = 'Home Affairs Verification';
GO

UPDATE dbo.Applications
SET CurrentStage = 'CIPC verification'
WHERE CurrentStage = 'Home Affairs Verification';
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

MERGE dbo.Tariffs AS target
USING
(
    VALUES
        ('Formal Business', 'New', 850.00),
        ('Formal Business', 'New Application', 850.00),
        ('Formal Business', 'Renewal', 550.00),
        ('Formal Business - New Application', 'New', 850.00),
        ('Formal Business - Renewal', 'Renewal', 550.00),
        ('Hawkers Licence', 'New', 250.00),
        ('Hawkers Licence', 'Renewal', 175.00),
        ('Trade Stand Permit', 'New', 320.00),
        ('Trade Stand Permit', 'Renewal', 220.00),
        ('Food Vending Licence', 'New', 400.00),
        ('Food Vending Licence', 'Renewal', 280.00),
        ('Event Licence', 'New', 650.00),
        ('Event Licence', 'Renewal', 450.00)
) AS source (LicenceType, ApplicationKind, FeeAmount)
ON target.LicenceType = source.LicenceType
   AND target.ApplicationKind = source.ApplicationKind
WHEN MATCHED THEN
    UPDATE SET FeeAmount = source.FeeAmount, IsActive = 1, ModifiedDate = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (LicenceType, ApplicationKind, FeeAmount)
    VALUES (source.LicenceType, source.ApplicationKind, source.FeeAmount);
GO

INSERT dbo.BusinessTypes (BusinessTypeName, Description)
SELECT DISTINCT
    NULLIF(LTRIM(RTRIM(applications.LicenceType)), '') AS BusinessTypeName,
    'Migrated from Applications.LicenceType'
FROM dbo.Applications applications
WHERE NULLIF(LTRIM(RTRIM(applications.LicenceType)), '') IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.BusinessTypes businessTypes
      WHERE businessTypes.BusinessTypeName = NULLIF(LTRIM(RTRIM(applications.LicenceType)), '')
  );
GO

MERGE dbo.lu_application_status AS target
USING
(
    VALUES
        ('Submitted', 'Application has been submitted', 10),
        ('Pending', 'Application is waiting for action', 20),
        ('In progress', 'Application is being attended to', 30),
        ('Approved', 'Application has been approved', 40),
        ('Rejected', 'Application has been rejected', 50),
        ('Resubmitted', 'Rejected application has been resubmitted', 60),
        ('Archived', 'Application has been archived', 90)
) AS source (StatusName, Description, SortOrder)
ON target.StatusName = source.StatusName
WHEN MATCHED THEN
    UPDATE SET Description = source.Description, SortOrder = source.SortOrder, IsActive = 1
WHEN NOT MATCHED THEN
    INSERT (StatusName, Description, SortOrder) VALUES (source.StatusName, source.Description, source.SortOrder);
GO

INSERT dbo.lu_application_status (StatusName, Description, SortOrder)
SELECT DISTINCT
    NULLIF(LTRIM(RTRIM(applications.Status)), '') AS StatusName,
    'Migrated from Applications.Status',
    80
FROM dbo.Applications applications
WHERE NULLIF(LTRIM(RTRIM(applications.Status)), '') IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.lu_application_status statuses
      WHERE statuses.StatusName = NULLIF(LTRIM(RTRIM(applications.Status)), '')
  );
GO

UPDATE applications
SET BusinessTypeId = businessTypes.BusinessTypeId
FROM dbo.Applications applications
INNER JOIN dbo.BusinessTypes businessTypes ON businessTypes.BusinessTypeName = applications.LicenceType
WHERE applications.BusinessTypeId IS NULL;
GO

UPDATE applications
SET Application_Status_id = statuses.Application_Status_id
FROM dbo.Applications applications
INNER JOIN dbo.lu_application_status statuses ON statuses.StatusName = applications.Status
WHERE applications.Application_Status_id IS NULL;
GO

MERGE dbo.Groups AS target
USING
(
    VALUES
        ('Super User', 'Full system administration access'),
        ('Admin Officer', 'Stall management and administrative application processing permission'),
        ('Licensing Officer', 'Application intake and final licence decision processing'),
        ('Compliance Officer', 'Compliance intake and application decision processing'),
        ('Metro Police', 'Metro Police inspection and enforcement permission'),
        ('City Planning', 'Zoning and planning review'),
        ('Health Department', 'Health report review'),
        ('Fire Department', 'Fire report review'),
        ('Senior Specialist', 'Senior specialist application review permission'),
        ('Functional Head', 'Functional head review and approval permission'),
        ('Director', 'Director review and approval permission'),
        ('Customer', 'Customer applicant access for making and managing applications')
) AS source (GroupName, Description)
ON target.GroupName = source.GroupName
WHEN MATCHED THEN
    UPDATE SET Description = source.Description, IsActive = 1
WHEN NOT MATCHED THEN
    INSERT (GroupName, Description) VALUES (source.GroupName, source.Description);
GO

DECLARE @UserId INT;
DECLARE @SuperUserGroupId INT;

MERGE dbo.Users AS target
USING
(
    SELECT
        'VuyiswaMa@tshwane.gov.za' AS Username,
        'Vuyiswa Ma' AS DisplayName,
        'VuyiswaMa@tshwane.gov.za' AS EmailAddress,
        'jLX/rYJKz4hjtuNrIqLHTJ6OYD4TzNTRXUgCwmb4aQ8=' AS PasswordHash,
        '49RUjXaRThBzWAkG/4oiXg==' AS PasswordSalt,
        210000 AS PasswordIterations
) AS source
ON target.Username = source.Username
WHEN MATCHED THEN
    UPDATE SET
        DisplayName = source.DisplayName,
        EmailAddress = source.EmailAddress,
        PasswordHash = source.PasswordHash,
        PasswordSalt = source.PasswordSalt,
        PasswordIterations = source.PasswordIterations,
        IsActive = 1,
        ModifiedDate = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Username, DisplayName, EmailAddress, PasswordHash, PasswordSalt, PasswordIterations)
    VALUES (source.Username, source.DisplayName, source.EmailAddress, source.PasswordHash, source.PasswordSalt, source.PasswordIterations);

SELECT @UserId = UserId FROM dbo.Users WHERE Username = 'VuyiswaMa@tshwane.gov.za';
SELECT @SuperUserGroupId = GroupId FROM dbo.Groups WHERE GroupName = 'Super User';

IF NOT EXISTS (SELECT 1 FROM dbo.UserGroups WHERE UserId = @UserId AND GroupId = @SuperUserGroupId)
BEGIN
    INSERT dbo.UserGroups (UserId, GroupId) VALUES (@UserId, @SuperUserGroupId);
END;
GO
