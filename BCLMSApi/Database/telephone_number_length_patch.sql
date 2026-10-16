-- Customer registration accepts international telephone numbers, including the +27 format.
-- Keep this column wide enough for the normalized value stored by AuthService.
IF COL_LENGTH('dbo.Users', 'TelephoneNumber') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD TelephoneNumber NVARCHAR(40) NULL;
END
ELSE
BEGIN
    ALTER TABLE dbo.Users ALTER COLUMN TelephoneNumber NVARCHAR(40) NULL;
END;
GO
