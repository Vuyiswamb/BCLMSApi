IF OBJECT_ID('dbo.OverdueApplicationReminderLog', 'U') IS NULL
BEGIN
 CREATE TABLE dbo.OverdueApplicationReminderLog (OverdueApplicationReminderLogId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OverdueApplicationReminderLog PRIMARY KEY, ApplicationId INT NOT NULL, UserId INT NOT NULL, ReminderDate DATE NOT NULL, SentDate DATETIME2(0) NOT NULL CONSTRAINT DF_OverdueReminder_SentDate DEFAULT SYSUTCDATETIME(), CONSTRAINT UQ_OverdueReminder_ApplicationUserDay UNIQUE(ApplicationId,UserId,ReminderDate), CONSTRAINT FK_OverdueReminder_Application FOREIGN KEY(ApplicationId) REFERENCES dbo.Applications(ApplicationId), CONSTRAINT FK_OverdueReminder_User FOREIGN KEY(UserId) REFERENCES dbo.Users(UserId));
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_GetOverdueApplicationReminderRecipients
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        a.ApplicationId,
        a.TrackingNumber,
        a.BusinessName,
        a.CurrentStage,
        u.UserId,
        u.DisplayName,
        u.EmailAddress
    FROM dbo.Applications AS a
    INNER JOIN dbo.ApplicationWorkflowSteps AS ws ON ws.ApplicationId = a.ApplicationId
    INNER JOIN dbo.UserGroups AS ug ON ug.GroupId = ws.AssignedGroupId
    INNER JOIN dbo.Users AS u ON u.UserId = ug.UserId AND u.IsActive = 1
    WHERE a.SubmittedDate < DATEADD(DAY, -21, SYSUTCDATETIME())
      AND LOWER(ISNULL(a.Status, '')) NOT IN ('approved', 'completed', 'complete', 'licence issued', 'rejected', 'cancelled', 'canceled')
      AND LOWER(ISNULL(ws.Status, '')) IN ('pending', 'in progress')
      AND NULLIF(LTRIM(RTRIM(u.EmailAddress)), '') IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.OverdueApplicationReminderLog AS logEntry
          WHERE logEntry.ApplicationId = a.ApplicationId
            AND logEntry.UserId = u.UserId
            AND logEntry.ReminderDate = CONVERT(date, SYSUTCDATETIME())
      );
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_RecordOverdueApplicationReminder
    @ApplicationId INT,
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        INSERT INTO dbo.OverdueApplicationReminderLog (ApplicationId, UserId, ReminderDate)
        VALUES (@ApplicationId, @UserId, CONVERT(date, SYSUTCDATETIME()));
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (2601, 2627)
            THROW;
    END CATCH;
END;
GO
