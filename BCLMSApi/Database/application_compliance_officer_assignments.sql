IF OBJECT_ID(N'dbo.ApplicationComplianceOfficerAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApplicationComplianceOfficerAssignments
    (
        AssignmentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApplicationComplianceOfficerAssignments PRIMARY KEY,
        ApplicationId INT NOT NULL,
        ComplianceOfficerUserId INT NOT NULL,
        ProcessStepName NVARCHAR(200) NOT NULL,
        AssignedByUserId INT NULL,
        AssignedDateUtc DATETIME2(0) NOT NULL CONSTRAINT DF_ApplicationComplianceOfficerAssignments_AssignedDateUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_ApplicationComplianceOfficerAssignments_Application FOREIGN KEY (ApplicationId) REFERENCES dbo.Applications(ApplicationId),
        CONSTRAINT FK_ApplicationComplianceOfficerAssignments_ComplianceOfficer FOREIGN KEY (ComplianceOfficerUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_ApplicationComplianceOfficerAssignments_AssignedBy FOREIGN KEY (AssignedByUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT UQ_ApplicationComplianceOfficerAssignments_ApplicationOfficerStep UNIQUE (ApplicationId, ComplianceOfficerUserId, ProcessStepName)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ApplicationComplianceOfficerAssignments_ApplicationId' AND object_id = OBJECT_ID(N'dbo.ApplicationComplianceOfficerAssignments'))
    CREATE INDEX IX_ApplicationComplianceOfficerAssignments_ApplicationId ON dbo.ApplicationComplianceOfficerAssignments(ApplicationId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ApplicationComplianceOfficerAssignments_ComplianceOfficerUserId' AND object_id = OBJECT_ID(N'dbo.ApplicationComplianceOfficerAssignments'))
    CREATE INDEX IX_ApplicationComplianceOfficerAssignments_ComplianceOfficerUserId ON dbo.ApplicationComplianceOfficerAssignments(ComplianceOfficerUserId);
GO
