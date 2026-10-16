SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.WorkflowTemplates', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkflowTemplates
    (
        WorkflowTemplateId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkflowTemplates PRIMARY KEY,
        ApplicationType NVARCHAR(100) NOT NULL CONSTRAINT UQ_WorkflowTemplates_ApplicationType UNIQUE,
        TemplateName NVARCHAR(150) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_WorkflowTemplates_IsActive DEFAULT (1),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_WorkflowTemplates_CreatedDate DEFAULT (SYSUTCDATETIME()),
        ModifiedDate DATETIME2(0) NULL,
        ModifiedByUserId INT NULL
    );
END;
GO

IF OBJECT_ID('dbo.WorkflowTemplateSteps', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkflowTemplateSteps
    (
        WorkflowTemplateStepId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkflowTemplateSteps PRIMARY KEY,
        WorkflowTemplateId INT NOT NULL,
        StepName NVARCHAR(120) NOT NULL,
        AssignedGroupId INT NOT NULL,
        SequenceNumber INT NOT NULL,
        InitialStatus NVARCHAR(40) NOT NULL CONSTRAINT DF_WorkflowTemplateSteps_InitialStatus DEFAULT ('Pending'),
        IsRequired BIT NOT NULL CONSTRAINT DF_WorkflowTemplateSteps_IsRequired DEFAULT (1),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_WorkflowTemplateSteps_CreatedDate DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_WorkflowTemplateSteps_Template FOREIGN KEY (WorkflowTemplateId) REFERENCES dbo.WorkflowTemplates(WorkflowTemplateId),
        CONSTRAINT FK_WorkflowTemplateSteps_Group FOREIGN KEY (AssignedGroupId) REFERENCES dbo.Groups(GroupId),
        CONSTRAINT UQ_WorkflowTemplateSteps_Order UNIQUE (WorkflowTemplateId, SequenceNumber)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.WorkflowTemplates WHERE ApplicationType = 'Formal Business') INSERT dbo.WorkflowTemplates (ApplicationType, TemplateName) VALUES ('Formal Business', 'Formal Business workflow');
IF NOT EXISTS (SELECT 1 FROM dbo.WorkflowTemplates WHERE ApplicationType = 'Hawkers Licence') INSERT dbo.WorkflowTemplates (ApplicationType, TemplateName) VALUES ('Hawkers Licence', 'Hawkers Licence workflow');
IF NOT EXISTS (SELECT 1 FROM dbo.WorkflowTemplates WHERE ApplicationType = 'Trade Stand Permit') INSERT dbo.WorkflowTemplates (ApplicationType, TemplateName) VALUES ('Trade Stand Permit', 'Trade Stand Permit workflow');
IF NOT EXISTS (SELECT 1 FROM dbo.WorkflowTemplates WHERE ApplicationType = 'Food Vending Licence') INSERT dbo.WorkflowTemplates (ApplicationType, TemplateName) VALUES ('Food Vending Licence', 'Food Vending Licence workflow');
IF NOT EXISTS (SELECT 1 FROM dbo.WorkflowTemplates WHERE ApplicationType = 'Event Licence') INSERT dbo.WorkflowTemplates (ApplicationType, TemplateName) VALUES ('Event Licence', 'Event Licence workflow');
GO

DECLARE @TemplateId INT;
SELECT @TemplateId = WorkflowTemplateId FROM dbo.WorkflowTemplates WHERE ApplicationType = 'Formal Business';
IF NOT EXISTS (SELECT 1 FROM dbo.WorkflowTemplateSteps WHERE WorkflowTemplateId = @TemplateId)
BEGIN
    INSERT dbo.WorkflowTemplateSteps (WorkflowTemplateId, StepName, AssignedGroupId, SequenceNumber)
    SELECT @TemplateId, steps.StepName, groups.GroupId, steps.SequenceNumber
    FROM (VALUES ('Application intake','Compliance Officer',1),('Proof of payment verification','Compliance Officer',2),('CIPC verification','Compliance Officer',3),('Zoning verification','City Planning',4),('Health report','Health Department',5),('Fire report','Fire Department',6),('Senior specialist review','Senior Specialist',7),('Admin Approval','Admin Officer',8),('Functional Head Approval','Functional Head',9),('Director Approval','Director',10),('Licence Issued','Compliance Officer',11)) steps(StepName,GroupName,SequenceNumber)
    INNER JOIN dbo.Groups groups ON groups.GroupName = steps.GroupName;
END;

DECLARE @ApplicationType NVARCHAR(100);
DECLARE template_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT ApplicationType FROM dbo.WorkflowTemplates WHERE ApplicationType <> 'Formal Business';
OPEN template_cursor; FETCH NEXT FROM template_cursor INTO @ApplicationType;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @TemplateId = WorkflowTemplateId FROM dbo.WorkflowTemplates WHERE ApplicationType = @ApplicationType;
    IF NOT EXISTS (SELECT 1 FROM dbo.WorkflowTemplateSteps WHERE WorkflowTemplateId = @TemplateId)
    BEGIN
        INSERT dbo.WorkflowTemplateSteps (WorkflowTemplateId, StepName, AssignedGroupId, SequenceNumber, InitialStatus)
        SELECT @TemplateId, steps.StepName, groups.GroupId, steps.SequenceNumber, steps.InitialStatus
        FROM (VALUES ('Workshop','Compliance Officer',1,'Approved'),('Application Submitted','Compliance Officer',2,'Pending'),('Home Affairs Verification','Compliance Officer',3,'Pending'),('TMPD Inspection (Site Inspection)','Metro Police',4,'Pending'),('Admin Approval','Admin Officer',5,'Pending'),('Functional Head Approval','Functional Head',6,'Pending'),('Director Approval','Director',7,'Pending'),('Licence Issued','Compliance Officer',8,'Pending')) steps(StepName,GroupName,SequenceNumber,InitialStatus)
        INNER JOIN dbo.Groups groups ON groups.GroupName = steps.GroupName;
    END;
    FETCH NEXT FROM template_cursor INTO @ApplicationType;
END;
CLOSE template_cursor; DEALLOCATE template_cursor;
GO

CREATE OR ALTER PROCEDURE dbo.usp_WorkflowTemplates_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT templates.WorkflowTemplateId, templates.ApplicationType, templates.TemplateName, templates.IsActive, steps.WorkflowTemplateStepId, steps.StepName, groups.GroupName AS AssignedGroupName, steps.SequenceNumber, steps.InitialStatus, steps.IsRequired
    FROM dbo.WorkflowTemplates templates
    LEFT JOIN dbo.WorkflowTemplateSteps steps ON steps.WorkflowTemplateId = templates.WorkflowTemplateId
    LEFT JOIN dbo.Groups groups ON groups.GroupId = steps.AssignedGroupId
    ORDER BY templates.ApplicationType, steps.SequenceNumber;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_WorkflowTemplates_GetApplicationTypes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ApplicationType FROM dbo.WorkflowTemplates
    UNION
    SELECT BusinessTypeName FROM dbo.BusinessTypes WHERE IsActive = 1
    ORDER BY ApplicationType;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_WorkflowTemplates_Save
    @WorkflowTemplateId INT = NULL, @ApplicationType NVARCHAR(100), @TemplateName NVARCHAR(150), @IsActive BIT, @UserId INT, @StepsJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
    IF @WorkflowTemplateId IS NULL
    BEGIN
        INSERT dbo.WorkflowTemplates (ApplicationType, TemplateName, IsActive, ModifiedByUserId) VALUES (LTRIM(RTRIM(@ApplicationType)), LTRIM(RTRIM(@TemplateName)), @IsActive, @UserId);
        SET @WorkflowTemplateId = CONVERT(INT, SCOPE_IDENTITY());
    END
    ELSE UPDATE dbo.WorkflowTemplates SET ApplicationType = LTRIM(RTRIM(@ApplicationType)), TemplateName = LTRIM(RTRIM(@TemplateName)), IsActive = @IsActive, ModifiedDate = SYSUTCDATETIME(), ModifiedByUserId = @UserId WHERE WorkflowTemplateId = @WorkflowTemplateId;
    DELETE FROM dbo.WorkflowTemplateSteps WHERE WorkflowTemplateId = @WorkflowTemplateId;
    INSERT dbo.WorkflowTemplateSteps (WorkflowTemplateId, StepName, AssignedGroupId, SequenceNumber, InitialStatus, IsRequired)
    SELECT @WorkflowTemplateId, jsonSteps.StepName, groups.GroupId, jsonSteps.SequenceNumber, jsonSteps.InitialStatus, jsonSteps.IsRequired
    FROM OPENJSON(@StepsJson) WITH (StepName NVARCHAR(120) '$.StepName', AssignedGroupName NVARCHAR(80) '$.AssignedGroupName', SequenceNumber INT '$.SequenceNumber', InitialStatus NVARCHAR(40) '$.InitialStatus', IsRequired BIT '$.IsRequired') jsonSteps
    INNER JOIN dbo.Groups groups ON groups.GroupName = jsonSteps.AssignedGroupName;

    -- Existing applications keep workflow status/audit data, but the template is
    -- the source of truth for all unarchived applications. Keep completed steps
    -- and their audit history, then rebuild the remaining steps from the template.
    DECLARE @ApplicationId INT;
    DECLARE @CompletedPrefix INT;
    DECLARE application_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT applications.ApplicationId
        FROM dbo.Applications applications
        WHERE applications.LicenceType = LTRIM(RTRIM(@ApplicationType))
          AND applications.Archive_Date IS NULL;

    OPEN application_cursor;
    FETCH NEXT FROM application_cursor INTO @ApplicationId;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SELECT @CompletedPrefix = COALESCE(MAX(SequenceNumber), 0)
        FROM dbo.ApplicationWorkflowSteps
        WHERE ApplicationId = @ApplicationId
          AND (Status IN ('Approved', 'Completed') OR CompletedDate IS NOT NULL);

        DELETE FROM dbo.ApplicationWorkflowSteps
        WHERE ApplicationId = @ApplicationId
          AND SequenceNumber > @CompletedPrefix;

        INSERT dbo.ApplicationWorkflowSteps (ApplicationId, StepName, AssignedGroupId, SequenceNumber, Status, StartedDate, CompletedDate)
        SELECT @ApplicationId,
               steps.StepName,
               steps.AssignedGroupId,
               steps.SequenceNumber,
               steps.InitialStatus,
               CASE WHEN steps.InitialStatus = 'Approved' THEN GETDATE() ELSE NULL END,
               CASE WHEN steps.InitialStatus = 'Approved' THEN GETDATE() ELSE NULL END
        FROM dbo.WorkflowTemplateSteps steps
        WHERE steps.WorkflowTemplateId = @WorkflowTemplateId
          AND steps.SequenceNumber > @CompletedPrefix;

        UPDATE applications
        SET CurrentStage = COALESCE(
            (SELECT TOP 1 steps.StepName
             FROM dbo.WorkflowTemplateSteps steps
             WHERE steps.WorkflowTemplateId = @WorkflowTemplateId
               AND steps.SequenceNumber > @CompletedPrefix
               AND steps.InitialStatus NOT IN ('Approved', 'Completed')
             ORDER BY steps.SequenceNumber),
            'Completed'),
            ModifiedDate = GETDATE()
        FROM dbo.Applications applications
        WHERE applications.ApplicationId = @ApplicationId;

        FETCH NEXT FROM application_cursor INTO @ApplicationId;
    END;
    CLOSE application_cursor;
    DEALLOCATE application_cursor;

    COMMIT TRANSACTION;
    SELECT templates.WorkflowTemplateId, templates.ApplicationType, templates.TemplateName, templates.IsActive, steps.WorkflowTemplateStepId, steps.StepName, groups.GroupName AS AssignedGroupName, steps.SequenceNumber, steps.InitialStatus, steps.IsRequired
    FROM dbo.WorkflowTemplates templates INNER JOIN dbo.WorkflowTemplateSteps steps ON steps.WorkflowTemplateId = templates.WorkflowTemplateId INNER JOIN dbo.Groups groups ON groups.GroupId = steps.AssignedGroupId
    WHERE templates.WorkflowTemplateId = @WorkflowTemplateId ORDER BY steps.SequenceNumber;
END;
GO
