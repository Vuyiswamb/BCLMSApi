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
