SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.WorkflowTemplateSteps', 'RequiresSignature') IS NULL
BEGIN
    ALTER TABLE dbo.WorkflowTemplateSteps
        ADD RequiresSignature BIT NOT NULL
            CONSTRAINT DF_WorkflowTemplateSteps_RequiresSignature DEFAULT (0) WITH VALUES;
END;

IF COL_LENGTH('dbo.ApplicationWorkflowSteps', 'RequiresSignature') IS NULL
BEGIN
    ALTER TABLE dbo.ApplicationWorkflowSteps
        ADD RequiresSignature BIT NOT NULL
            CONSTRAINT DF_ApplicationWorkflowSteps_RequiresSignature DEFAULT (0) WITH VALUES;
END;

-- Preserve the existing BCLMS approval policy for templates created before
-- the flag existed. New steps remain optional unless the template editor sets
-- RequiresSignature = 1.
-- Use dynamic SQL because SQL Server can compile the whole batch before it
-- recognizes columns added by ALTER TABLE above.
EXEC sys.sp_executesql N'
    UPDATE dbo.WorkflowTemplateSteps
    SET RequiresSignature = 1
    WHERE LOWER(LTRIM(RTRIM(StepName))) IN (''admin approval'', ''admin approval of payment'', ''functional head approval'', ''director approval'');

    UPDATE dbo.ApplicationWorkflowSteps
    SET RequiresSignature = 1
    WHERE LOWER(LTRIM(RTRIM(StepName))) IN (''admin approval'', ''admin approval of payment'', ''functional head approval'', ''director approval'');';

COMMIT TRANSACTION;
