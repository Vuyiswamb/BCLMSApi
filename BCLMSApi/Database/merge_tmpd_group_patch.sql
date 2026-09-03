DECLARE @KeepGroupId INT;
DECLARE @DuplicateGroupId INT;

SELECT @KeepGroupId = GroupId
FROM dbo.Groups
WHERE GroupName = 'Metro Police';

SELECT @DuplicateGroupId = GroupId
FROM dbo.Groups
WHERE GroupName = 'Tshwane Metro Police Department (TMPD)';

IF @KeepGroupId IS NOT NULL AND @DuplicateGroupId IS NOT NULL
BEGIN
    INSERT INTO dbo.UserGroups (UserId, GroupId)
    SELECT duplicateGroups.UserId, @KeepGroupId
    FROM dbo.UserGroups duplicateGroups
    WHERE duplicateGroups.GroupId = @DuplicateGroupId
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.UserGroups existingGroups
          WHERE existingGroups.UserId = duplicateGroups.UserId
            AND existingGroups.GroupId = @KeepGroupId
      );

    DELETE FROM dbo.UserGroups
    WHERE GroupId = @DuplicateGroupId;

    UPDATE dbo.Groups
    SET IsActive = 0
    WHERE GroupId = @DuplicateGroupId;
END;
GO
