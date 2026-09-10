SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.HomeAffairsIdentityLookups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HomeAffairsIdentityLookups
    (
        ApplicationId INT NOT NULL CONSTRAINT PK_HomeAffairsIdentityLookups PRIMARY KEY,
        IdNumber NVARCHAR(50) NOT NULL,
        ApplicantName NVARCHAR(150) NOT NULL,
        SourceEnvironment NVARCHAR(400) NOT NULL,
        IdentityJson NVARCHAR(MAX) NOT NULL,
        PhotoJpeg VARBINARY(MAX) NULL,
        HanisTransactionId NVARCHAR(120) NULL,
        LookedUpAtUtc DATETIME2(7) NOT NULL,
        ExpiresAtUtc DATETIME2(7) NOT NULL,
        LookedUpByUserId INT NOT NULL,
        CONSTRAINT FK_HomeAffairsIdentityLookups_Applications FOREIGN KEY (ApplicationId)
            REFERENCES dbo.Applications(ApplicationId),
        CONSTRAINT CK_HomeAffairsIdentityLookups_Json CHECK (ISJSON(IdentityJson) = 1),
        CONSTRAINT CK_HomeAffairsIdentityLookups_Expiry CHECK (ExpiresAtUtc > LookedUpAtUtc),
        CONSTRAINT CK_HomeAffairsIdentityLookups_PhotoSize CHECK (DATALENGTH(PhotoJpeg) <= 5242880)
    );
END;
COMMIT;
