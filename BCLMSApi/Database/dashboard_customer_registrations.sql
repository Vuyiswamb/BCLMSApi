CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_GetCustomerRegistrationCounts
AS
BEGIN
    SET NOCOUNT ON;

    SELECT YEAR(users.CreatedDate) AS [Year], MONTH(users.CreatedDate) AS [Month], COUNT(*) AS [Count]
    FROM dbo.Users users
    WHERE EXISTS
    (
        SELECT 1
        FROM dbo.UserGroups userGroups
        INNER JOIN dbo.Groups groups ON groups.GroupId = userGroups.GroupId
        WHERE userGroups.UserId = users.UserId
          AND groups.IsActive = 1
          AND groups.GroupName = 'Customer'
    )
    GROUP BY YEAR(users.CreatedDate), MONTH(users.CreatedDate)
    ORDER BY [Year], [Month];
END;
GO
