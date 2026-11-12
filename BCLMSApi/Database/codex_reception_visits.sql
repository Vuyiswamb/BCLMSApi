IF OBJECT_ID('dbo.ReceptionVisits', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReceptionVisits
    (
        ReceptionVisitId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReceptionVisits PRIMARY KEY,
        VisitDate DATE NOT NULL,
        FullName NVARCHAR(150) NOT NULL,
        ContactNumber NVARCHAR(40) NOT NULL,
        ServiceRequired NVARCHAR(50) NOT NULL,
        Referral NVARCHAR(200) NULL,
        CapturedByUserId INT NOT NULL,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_ReceptionVisits_CreatedDate DEFAULT(SYSUTCDATETIME())
    );
    CREATE INDEX IX_ReceptionVisits_VisitDate ON dbo.ReceptionVisits(VisitDate DESC, CreatedDate DESC);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ReceptionVisits_Delete
    @ReceptionVisitId INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.ReceptionVisits WHERE ReceptionVisitId = @ReceptionVisitId;
    SELECT CONVERT(INT, @@ROWCOUNT);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ReceptionVisits_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT visits.ReceptionVisitId, visits.VisitDate, visits.FullName, visits.ContactNumber, visits.ServiceRequired, visits.Referral,
           COALESCE(NULLIF(LTRIM(RTRIM(users.DisplayName)), ''), users.Username) AS CapturedBy, visits.CreatedDate
    FROM dbo.ReceptionVisits visits JOIN dbo.Users users ON users.UserId = visits.CapturedByUserId
    ORDER BY visits.VisitDate DESC, visits.CreatedDate DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ReceptionVisits_Create
    @VisitDate DATE, @FullName NVARCHAR(150), @ContactNumber NVARCHAR(40), @ServiceRequired NVARCHAR(50), @Referral NVARCHAR(200) = NULL, @CapturedByUserId INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.ReceptionVisits(VisitDate, FullName, ContactNumber, ServiceRequired, Referral, CapturedByUserId)
    VALUES(@VisitDate, @FullName, @ContactNumber, @ServiceRequired, @Referral, @CapturedByUserId);
    DECLARE @Id INT = CONVERT(INT, SCOPE_IDENTITY());
    SELECT visits.ReceptionVisitId, visits.VisitDate, visits.FullName, visits.ContactNumber, visits.ServiceRequired, visits.Referral,
           COALESCE(NULLIF(LTRIM(RTRIM(users.DisplayName)), ''), users.Username) AS CapturedBy, visits.CreatedDate
    FROM dbo.ReceptionVisits visits JOIN dbo.Users users ON users.UserId = visits.CapturedByUserId WHERE visits.ReceptionVisitId = @Id;
END;
GO
