/* Deploy this script before deploying the API.  Controllers must only call these procedures. */
IF OBJECT_ID('dbo.EventPremises', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EventPremises
    (
        EventPremiseId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EventPremises PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        StreetNameAndNumber NVARCHAR(500) NOT NULL,
        ContactDetails NVARCHAR(500) NOT NULL,
        Latitude DECIMAL(18,8) NOT NULL,
        Longitude DECIMAL(18,8) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_EventPremises_IsActive DEFAULT (1),
        CreatedByUserId INT NULL,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_EventPremises_CreatedDate DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL,
        UpdatedDate DATETIME2(0) NULL
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_InvoiceItems_GetAll AS
BEGIN
    SET NOCOUNT ON;
    SELECT InvoiceItemId, ItemCode, Description, GlAccountNumber, Amount, IsActive
    FROM dbo.InvoiceItems ORDER BY ItemCode;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_InvoiceItems_Save
    @InvoiceItemId INT = NULL, @ItemCode NVARCHAR(50), @Description NVARCHAR(250),
    @GlAccountNumber NVARCHAR(50), @Amount DECIMAL(18,2), @IsActive BIT
AS
BEGIN
    SET NOCOUNT ON;
    IF @InvoiceItemId IS NULL
    BEGIN
        INSERT dbo.InvoiceItems(ItemCode, Description, GlAccountNumber, Amount, IsActive)
        VALUES (@ItemCode, @Description, @GlAccountNumber, @Amount, @IsActive);
        SET @InvoiceItemId = CONVERT(INT, SCOPE_IDENTITY());
    END
    ELSE
    BEGIN
        UPDATE dbo.InvoiceItems SET ItemCode=@ItemCode, Description=@Description,
            GlAccountNumber=@GlAccountNumber, Amount=@Amount, IsActive=@IsActive,
            ModifiedDate=SYSUTCDATETIME() WHERE InvoiceItemId=@InvoiceItemId;
        IF @@ROWCOUNT = 0 RETURN;
    END
    SELECT InvoiceItemId, ItemCode, Description, GlAccountNumber, Amount, IsActive
    FROM dbo.InvoiceItems WHERE InvoiceItemId=@InvoiceItemId;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_InvoiceItems_Delete @InvoiceItemId INT AS
BEGIN
    SET NOCOUNT ON;
    DELETE dbo.InvoiceItems WHERE InvoiceItemId=@InvoiceItemId;
    SELECT CONVERT(BIT, CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Invoices_Save
    @ApplicationId INT, @PaymentReference NVARCHAR(100), @GlAccountNumber NVARCHAR(50), @LinesJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.ApplicationInvoices WHERE ApplicationId=@ApplicationId)
    BEGIN
        INSERT dbo.ApplicationInvoices(ApplicationId,PaymentReference,GlAccountNumber)
        VALUES(@ApplicationId,@PaymentReference,@GlAccountNumber);
        DECLARE @InvoiceId INT=CONVERT(INT,SCOPE_IDENTITY());
        INSERT dbo.ApplicationInvoiceLines(ApplicationInvoiceId,ItemCode,Description,Amount)
        SELECT @InvoiceId,ItemCode,Description,Amount
        FROM OPENJSON(@LinesJson) WITH(ItemCode NVARCHAR(50),Description NVARCHAR(250),Amount DECIMAL(18,2));
    END
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Invoices_Get @ApplicationId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.PaymentReference,i.GlAccountNumber,i.PaymentStatus,l.ItemCode,l.Description,l.Amount
    FROM dbo.ApplicationInvoices i JOIN dbo.ApplicationInvoiceLines l ON l.ApplicationInvoiceId=i.ApplicationInvoiceId
    WHERE i.ApplicationId=@ApplicationId;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Invoices_GetTransactions @ApplicationId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT PaymentReference,Amount,PaymentMonth,PaymentStatus,RecordedDate
    FROM dbo.ApplicationPaymentTransactions WHERE ApplicationId=@ApplicationId ORDER BY PaymentMonth DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Invoices_ConfirmPayment @ApplicationId INT, @UserId INT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Amount DECIMAL(18,2)=(SELECT SUM(l.Amount) FROM dbo.ApplicationInvoiceLines l JOIN dbo.ApplicationInvoices i ON i.ApplicationInvoiceId=l.ApplicationInvoiceId WHERE i.ApplicationId=@ApplicationId);
    DECLARE @Reference NVARCHAR(100)=(SELECT PaymentReference FROM dbo.ApplicationInvoices WHERE ApplicationId=@ApplicationId);
    UPDATE dbo.ApplicationInvoices SET PaymentStatus='Confirmed',ConfirmedDate=SYSUTCDATETIME(),ConfirmedByUserId=@UserId
    WHERE ApplicationId=@ApplicationId AND PaymentStatus<>'Confirmed';
    IF @@ROWCOUNT > 0
        INSERT dbo.ApplicationPaymentTransactions(ApplicationId,PaymentReference,Amount,PaymentMonth,RecordedByUserId)
        SELECT @ApplicationId,@Reference,@Amount,DATEFROMPARTS(YEAR(GETUTCDATE()),MONTH(GETUTCDATE()),1),@UserId
        WHERE NOT EXISTS (SELECT 1 FROM dbo.ApplicationPaymentTransactions WHERE ApplicationId=@ApplicationId AND PaymentMonth=DATEFROMPARTS(YEAR(GETUTCDATE()),MONTH(GETUTCDATE()),1));
    SELECT CONVERT(BIT, CASE WHEN @@ROWCOUNT > 0 OR EXISTS(SELECT 1 FROM dbo.ApplicationInvoices WHERE ApplicationId=@ApplicationId AND PaymentStatus='Confirmed') THEN 1 ELSE 0 END);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_EventPremises_GetAll AS
BEGIN SET NOCOUNT ON; SELECT EventPremiseId,Name,StreetNameAndNumber,ContactDetails,Latitude,Longitude,IsActive FROM dbo.EventPremises ORDER BY IsActive DESC,Name ASC; END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_EventPremises_Save
    @EventPremiseId INT=NULL,@Name NVARCHAR(200),@StreetNameAndNumber NVARCHAR(500),@ContactDetails NVARCHAR(500),@Latitude DECIMAL(18,8),@Longitude DECIMAL(18,8),@UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    IF @EventPremiseId IS NULL
    BEGIN INSERT dbo.EventPremises(Name,StreetNameAndNumber,ContactDetails,Latitude,Longitude,IsActive,CreatedByUserId) VALUES(@Name,@StreetNameAndNumber,@ContactDetails,@Latitude,@Longitude,1,@UserId); SET @EventPremiseId=CONVERT(INT,SCOPE_IDENTITY()); END
    ELSE UPDATE dbo.EventPremises SET Name=@Name,StreetNameAndNumber=@StreetNameAndNumber,ContactDetails=@ContactDetails,Latitude=@Latitude,Longitude=@Longitude,UpdatedByUserId=@UserId,UpdatedDate=SYSUTCDATETIME() WHERE EventPremiseId=@EventPremiseId;
    SELECT EventPremiseId,Name,StreetNameAndNumber,ContactDetails,Latitude,Longitude,IsActive FROM dbo.EventPremises WHERE EventPremiseId=@EventPremiseId;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_EventPremises_Disable @EventPremiseId INT,@UserId INT AS
BEGIN SET NOCOUNT ON; UPDATE dbo.EventPremises SET IsActive=0,UpdatedByUserId=@UserId,UpdatedDate=SYSUTCDATETIME() WHERE EventPremiseId=@EventPremiseId; SELECT CONVERT(BIT,CASE WHEN @@ROWCOUNT>0 THEN 1 ELSE 0 END); END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_GetCustomers AS
BEGIN
    SET NOCOUNT ON;
    WITH CustomerLocations AS
    (
        SELECT b.UserId,b.BusinessName,b.TownshipId,a.RegionName,a.Latitude,a.Longitude FROM dbo.CustomerBusinesses b
        OUTER APPLY (SELECT TOP (1) x.RegionName,x.Latitude,x.Longitude FROM dbo.Applications x WHERE x.BusinessId=b.BusinessId AND x.UserId=b.UserId ORDER BY x.SubmittedDate DESC,x.ApplicationId DESC) a WHERE b.IsActive=1
        UNION ALL SELECT a.UserId,a.BusinessName,a.TownshipId,a.RegionName,a.Latitude,a.Longitude FROM dbo.Applications a WHERE a.BusinessId IS NULL
    )
    SELECT u.UserId,l.BusinessName,l.TownshipId,l.RegionName,l.Latitude,l.Longitude FROM dbo.Users u LEFT JOIN CustomerLocations l ON l.UserId=u.UserId
    WHERE EXISTS (SELECT 1 FROM dbo.UserGroups ug JOIN dbo.Groups g ON g.GroupId=ug.GroupId WHERE ug.UserId=u.UserId AND g.GroupName='Customer') ORDER BY u.UserId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_WorkshopAttendanceRequests_GetInternal AS
BEGIN SET NOCOUNT ON;
    SELECT requests.WorkshopAttendanceRequestId,requests.BusinessId,requests.UserId,requests.RequestedLicenceType,requests.WorkshopMode,requests.Status,requests.RequestedDate,requests.ScheduledWorkshopDate,requests.InvitedDate,requests.AttendedDate,requests.Notes,businesses.BusinessName,COALESCE(NULLIF(users.DisplayName,''),users.Username) AS OwnerName,COALESCE(NULLIF(users.EmailAddress,''),users.Username) AS EmailAddress,COALESCE(businesses.TelephoneNumber,'') AS MobileNumber,NULLIF(LTRIM(RTRIM(CONVERT(NVARCHAR(100),townships.REGION))),'') AS RegionName,townships.TOWNSHIP AS TownshipName
    FROM dbo.WorkshopAttendanceRequests requests JOIN dbo.CustomerBusinesses businesses ON businesses.BusinessId=requests.BusinessId JOIN dbo.Users users ON users.UserId=requests.UserId LEFT JOIN dbo.TOWNSHIPS townships ON townships.ID=businesses.TownshipId
    WHERE requests.IsActive=1 ORDER BY CASE requests.Status WHEN 'Requested' THEN 0 WHEN 'Date Set' THEN 1 WHEN 'Invited' THEN 2 WHEN 'Attended' THEN 3 ELSE 4 END,requests.RequestedDate DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_WorkshopAttendanceRequests_GetMine @UserId INT AS
BEGIN SET NOCOUNT ON;
    SELECT requests.WorkshopAttendanceRequestId,requests.BusinessId,requests.UserId,requests.RequestedLicenceType,requests.WorkshopMode,requests.Status,requests.RequestedDate,requests.ScheduledWorkshopDate,requests.InvitedDate,requests.AttendedDate,requests.Notes,businesses.BusinessName,COALESCE(NULLIF(users.DisplayName,''),users.Username) AS OwnerName,COALESCE(NULLIF(users.EmailAddress,''),users.Username) AS EmailAddress,COALESCE(businesses.TelephoneNumber,'') AS MobileNumber,NULLIF(LTRIM(RTRIM(CONVERT(NVARCHAR(100),townships.REGION))),'') AS RegionName,townships.TOWNSHIP AS TownshipName
    FROM dbo.WorkshopAttendanceRequests requests JOIN dbo.CustomerBusinesses businesses ON businesses.BusinessId=requests.BusinessId JOIN dbo.Users users ON users.UserId=requests.UserId LEFT JOIN dbo.TOWNSHIPS townships ON townships.ID=businesses.TownshipId
    WHERE requests.IsActive=1 AND requests.UserId=@UserId ORDER BY requests.RequestedDate DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_WorkshopAttendanceRequests_GetById @RequestId INT AS
BEGIN SET NOCOUNT ON;
    SELECT requests.WorkshopAttendanceRequestId,requests.BusinessId,requests.UserId,requests.RequestedLicenceType,requests.WorkshopMode,requests.Status,requests.RequestedDate,requests.ScheduledWorkshopDate,requests.InvitedDate,requests.AttendedDate,requests.Notes,businesses.BusinessName,COALESCE(NULLIF(users.DisplayName,''),users.Username) AS OwnerName,COALESCE(NULLIF(users.EmailAddress,''),users.Username) AS EmailAddress,COALESCE(businesses.TelephoneNumber,'') AS MobileNumber,NULLIF(LTRIM(RTRIM(CONVERT(NVARCHAR(100),townships.REGION))),'') AS RegionName,townships.TOWNSHIP AS TownshipName
    FROM dbo.WorkshopAttendanceRequests requests JOIN dbo.CustomerBusinesses businesses ON businesses.BusinessId=requests.BusinessId JOIN dbo.Users users ON users.UserId=requests.UserId LEFT JOIN dbo.TOWNSHIPS townships ON townships.ID=businesses.TownshipId
    WHERE requests.WorkshopAttendanceRequestId=@RequestId AND requests.IsActive=1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_WorkshopAttendanceRequests_Create @BusinessId INT,@UserId INT,@RequestedLicenceType NVARCHAR(250),@WorkshopMode NVARCHAR(50),@Notes NVARCHAR(MAX)=NULL AS
BEGIN SET NOCOUNT ON;
    IF NOT EXISTS(SELECT 1 FROM dbo.CustomerBusinesses WHERE BusinessId=@BusinessId AND UserId=@UserId AND IsActive=1) THROW 51001,'Business not found for this customer.',1;
    IF EXISTS(SELECT 1 FROM dbo.CustomerBusinesses WHERE BusinessId=@BusinessId AND COALESCE(WorkshopAttended,0)=1) THROW 51002,'Workshop attendance has already been recorded for this business.',1;
    IF EXISTS(SELECT 1 FROM dbo.WorkshopAttendanceRequests WHERE BusinessId=@BusinessId AND IsActive=1 AND Status IN('Requested','Date Set','Invited','Attended')) THROW 51003,'A workshop request already exists for this business.',1;
    INSERT dbo.WorkshopAttendanceRequests(BusinessId,UserId,RequestedLicenceType,WorkshopMode,Status,RequestedDate,Notes,IsActive,CreatedDate) VALUES(@BusinessId,@UserId,@RequestedLicenceType,@WorkshopMode,'Requested',SYSUTCDATETIME(),@Notes,1,SYSUTCDATETIME());
    SELECT CONVERT(INT,SCOPE_IDENTITY());
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_WorkshopAttendanceRequests_MarkAttended @RequestId INT AS
BEGIN SET NOCOUNT ON; DECLARE @BusinessId INT=(SELECT BusinessId FROM dbo.WorkshopAttendanceRequests WHERE WorkshopAttendanceRequestId=@RequestId AND IsActive=1); IF @BusinessId IS NULL BEGIN SELECT CONVERT(BIT,0); RETURN; END; UPDATE dbo.WorkshopAttendanceRequests SET Status='Attended',AttendedDate=COALESCE(AttendedDate,SYSUTCDATETIME()),ModifiedDate=SYSUTCDATETIME() WHERE WorkshopAttendanceRequestId=@RequestId; UPDATE dbo.CustomerBusinesses SET WorkshopAttended=1,WorkshopAttendedDate=COALESCE(WorkshopAttendedDate,SYSUTCDATETIME()),ModifiedDate=SYSUTCDATETIME() WHERE BusinessId=@BusinessId; SELECT CONVERT(BIT,1); END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_WorkshopAttendanceRequests_Schedule @RequestId INT,@ScheduledWorkshopDate DATETIME2 AS
BEGIN SET NOCOUNT ON; UPDATE dbo.WorkshopAttendanceRequests SET ScheduledWorkshopDate=@ScheduledWorkshopDate,Status='Date Set',ModifiedDate=SYSUTCDATETIME() WHERE WorkshopAttendanceRequestId=@RequestId AND IsActive=1 AND Status<>'Attended'; SELECT @@ROWCOUNT; END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_WorkshopAttendanceRequests_MarkInvited @RequestId INT AS
BEGIN SET NOCOUNT ON; UPDATE dbo.WorkshopAttendanceRequests SET Status=CASE WHEN Status='Requested' THEN 'Invited' ELSE Status END,InvitedDate=COALESCE(InvitedDate,SYSUTCDATETIME()),ModifiedDate=SYSUTCDATETIME() WHERE WorkshopAttendanceRequestId=@RequestId AND IsActive=1; END;
GO

IF OBJECT_ID('dbo.PermitHolderPhotos','U') IS NULL
BEGIN
    CREATE TABLE dbo.PermitHolderPhotos(ApplicationId INT NOT NULL PRIMARY KEY REFERENCES dbo.Applications(ApplicationId),ContentType NVARCHAR(50) NOT NULL,FileContent VARBINARY(MAX) NOT NULL,UpdatedByUserId INT NOT NULL,UpdatedAtUtc DATETIME2 NOT NULL);
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_PermitHolderPhotos_Save @ApplicationId INT,@UserId INT,@ContentType NVARCHAR(50),@FileContent VARBINARY(MAX) AS
BEGIN SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION; DECLARE @LockResult INT; EXEC @LockResult=sys.sp_getapplock @Resource='PermitHolderPhotos',@LockMode='Exclusive',@LockOwner='Transaction'; IF @LockResult<0 THROW 50001,'Unable to save the permit photo. Please retry.',1; UPDATE dbo.PermitHolderPhotos SET ContentType=@ContentType,FileContent=@FileContent,UpdatedByUserId=@UserId,UpdatedAtUtc=SYSUTCDATETIME() WHERE ApplicationId=@ApplicationId; IF @@ROWCOUNT=0 INSERT dbo.PermitHolderPhotos VALUES(@ApplicationId,@ContentType,@FileContent,@UserId,SYSUTCDATETIME()); COMMIT; END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_PermitHolderPhotos_Get @ApplicationId INT,@IncludeContent BIT AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM dbo.PermitHolderPhotos WHERE ApplicationId=@ApplicationId AND DATALENGTH(FileContent)>0) BEGIN SELECT ContentType,CASE WHEN @IncludeContent=1 THEN FileContent ELSE NULL END AS FileContent FROM dbo.PermitHolderPhotos WHERE ApplicationId=@ApplicationId; RETURN; END; SELECT COALESCE(NULLIF(business.BusinessPhotoContentType,''),'image/jpeg') AS ContentType,CASE WHEN @IncludeContent=1 THEN business.BusinessPhotoContent ELSE NULL END AS FileContent FROM dbo.Applications application JOIN dbo.CustomerBusinesses business ON business.BusinessId=application.BusinessId WHERE application.ApplicationId=@ApplicationId AND application.Archive_Date IS NULL AND DATALENGTH(business.BusinessPhotoContent)>0; END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Auth_GetDiagnostics @Username NVARCHAR(320)=NULL AS
BEGIN SET NOCOUNT ON; SELECT CONVERT(BIT,CASE WHEN OBJECT_ID('dbo.usp_Auth_GetUserForLogin','P') IS NULL THEN 0 ELSE 1 END),CONVERT(BIT,CASE WHEN OBJECT_ID('dbo.usp_Auth_UpdateLastLogin','P') IS NULL THEN 0 ELSE 1 END); IF NULLIF(LTRIM(RTRIM(@Username)),'') IS NOT NULL SELECT TOP(1) users.UserId,users.IsActive,users.PasswordHash,users.PasswordSalt,users.PasswordIterations,(SELECT COUNT(*) FROM dbo.UserGroups userGroups WHERE userGroups.UserId=users.UserId) AS GroupCount FROM dbo.Users users WHERE users.Username=@Username OR users.EmailAddress=@Username; END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AssistedIntakes_List @StaffId INT AS
BEGIN SET NOCOUNT ON; SELECT TOP(50) intake.IntakeId,customer.DisplayName,customer.EmailAddress,intake.ApplicationId,application.TrackingNumber FROM dbo.AssistedIntakes intake JOIN dbo.Users customer ON customer.UserId=intake.CustomerUserId LEFT JOIN dbo.Applications application ON application.ApplicationId=intake.ApplicationId WHERE intake.OperatorUserId=@StaffId ORDER BY intake.IntakeId DESC; END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AssistedIntakes_FindCustomer @Email NVARCHAR(320) AS
BEGIN SET NOCOUNT ON; SELECT users.UserId FROM dbo.Users users WHERE users.EmailAddress=@Email AND users.IsActive=1 AND EXISTS(SELECT 1 FROM dbo.UserGroups ug JOIN dbo.Groups g ON g.GroupId=ug.GroupId WHERE ug.UserId=users.UserId AND g.GroupName='Customer') AND NOT EXISTS(SELECT 1 FROM dbo.UserGroups ug JOIN dbo.Groups g ON g.GroupId=ug.GroupId WHERE ug.UserId=users.UserId AND g.GroupName<>'Customer'); END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AssistedIntakes_Create @StaffId INT,@CustomerId INT,@IdNumber NVARCHAR(80),@Phone NVARCHAR(40) AS
BEGIN SET NOCOUNT ON; INSERT dbo.AssistedIntakes(OperatorUserId,CustomerUserId,IdNumber,Phone) OUTPUT INSERTED.IntakeId VALUES(@StaffId,@CustomerId,@IdNumber,@Phone); END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AssistedIntakes_Get @IntakeId INT,@StaffId INT AS
BEGIN SET NOCOUNT ON; SELECT intake.IntakeId,intake.CustomerUserId,customer.DisplayName,customer.EmailAddress,intake.IdNumber,intake.Phone,intake.BusinessId,intake.ApplicationId,intake.ApplicationResponse FROM dbo.AssistedIntakes intake WITH(UPDLOCK) JOIN dbo.Users customer ON customer.UserId=intake.CustomerUserId WHERE intake.IntakeId=@IntakeId AND intake.OperatorUserId=@StaffId AND customer.IsActive=1 AND EXISTS(SELECT 1 FROM dbo.UserGroups ug JOIN dbo.Groups g ON g.GroupId=ug.GroupId WHERE ug.UserId=customer.UserId AND g.GroupName='Customer') AND NOT EXISTS(SELECT 1 FROM dbo.UserGroups ug JOIN dbo.Groups g ON g.GroupId=ug.GroupId WHERE ug.UserId=customer.UserId AND g.GroupName<>'Customer'); END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AssistedIntakes_LinkBusiness @IntakeId INT,@BusinessId INT AS
BEGIN SET NOCOUNT ON; UPDATE dbo.AssistedIntakes SET BusinessId=@BusinessId,UpdatedAtUtc=SYSUTCDATETIME() WHERE IntakeId=@IntakeId; END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AssistedIntakes_RecordWorkshop @IntakeId INT,@CustomerId INT,@BusinessId INT AS
BEGIN SET NOCOUNT ON; UPDATE dbo.CustomerBusinesses SET WorkshopAttended=1,WorkshopAttendedDate=COALESCE(WorkshopAttendedDate,SYSUTCDATETIME()),ModifiedDate=SYSUTCDATETIME() WHERE BusinessId=@BusinessId AND UserId=@CustomerId; UPDATE dbo.WorkshopAttendanceRequests SET Status='Attended',AttendedDate=COALESCE(AttendedDate,SYSUTCDATETIME()),ModifiedDate=SYSUTCDATETIME() WHERE BusinessId=@BusinessId AND UserId=@CustomerId AND IsActive=1; UPDATE dbo.AssistedIntakes SET WorkshopRecordedAtUtc=COALESCE(WorkshopRecordedAtUtc,SYSUTCDATETIME()),UpdatedAtUtc=SYSUTCDATETIME() WHERE IntakeId=@IntakeId; END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AssistedIntakes_SaveApplication @IntakeId INT,@ApplicationId INT,@Response NVARCHAR(MAX) AS
BEGIN SET NOCOUNT ON; UPDATE dbo.AssistedIntakes SET ApplicationId=@ApplicationId,ApplicationResponse=@Response,UpdatedAtUtc=SYSUTCDATETIME() WHERE IntakeId=@IntakeId; END;
GO
