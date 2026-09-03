using System.Data;
using BCLMSApi.Models;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Data;

public class AuthRepository(Datalayer datalayer) : IAuthRepository
{
    public async Task<SystemUser?> GetUserForLoginAsync(string username)
    {

        try
        {  
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Auth_GetUserForLogin");
        command.Parameters.AddWithValue("@Username", username);

        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        SystemUser? user = null;
        while (await reader.ReadAsync())
        {
            user ??= new SystemUser
            {
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                Username = reader.GetString(reader.GetOrdinal("Username")),
                DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
                PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
                PasswordSalt = reader.GetString(reader.GetOrdinal("PasswordSalt")),
                PasswordIterations = reader.GetInt32(reader.GetOrdinal("PasswordIterations")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                HasAllRegions = HasColumn(reader, "HasAllRegions") && reader.GetBoolean(reader.GetOrdinal("HasAllRegions")),
            };

            if (!reader.IsDBNull(reader.GetOrdinal("GroupName")))
            {
                var groupName = reader.GetString(reader.GetOrdinal("GroupName"));
                if (!user.Groups.Contains(groupName, StringComparer.OrdinalIgnoreCase))
                {
                    user.Groups.Add(groupName);
                }
            }

            if (HasColumn(reader, "RegionName") && !reader.IsDBNull(reader.GetOrdinal("RegionName")))
            {
                var regionName = reader.GetString(reader.GetOrdinal("RegionName"));
                if (!user.Regions.Contains(regionName, StringComparer.OrdinalIgnoreCase))
                {
                    user.Regions.Add(regionName);
                }
            }
        }

        return user;
        }
        catch (Exception ex)
        {
            throw ex;
        }

    }


    public async Task<ResetUser?> GetUserByUsernameOrEmailAsync(string usernameOrEmail)
    {
        try
        {  
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Auth_GetUserByUsernameOrEmail");
        command.Parameters.AddWithValue("@UsernameOrEmail", usernameOrEmail);

        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new ResetUser
        {
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            Username = reader.GetString(reader.GetOrdinal("Username")),
            DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
            EmailAddress = reader.IsDBNull(reader.GetOrdinal("EmailAddress"))
                ? null
                : reader.GetString(reader.GetOrdinal("EmailAddress"))
        };

        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task UpdateLastLoginAsync(int userId)
    {

        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Auth_UpdateLastLogin");
            command.Parameters.AddWithValue("@UserId", userId);

            await command.Connection!.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }
        catch(Exception ex)
        {
            throw ex;
        } 
    }

    public async Task SavePasswordResetTokenAsync(int userId, string tokenHash, int hoursToLive, string? ipAddress)
    {
        try
        {  
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Auth_CreatePasswordResetToken");
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@TokenHash", tokenHash);
        command.Parameters.AddWithValue("@HoursToLive", hoursToLive);
        command.Parameters.AddWithValue("@RequestedIpAddress", string.IsNullOrWhiteSpace(ipAddress) ? DBNull.Value : ipAddress);

        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();

        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<PasswordResetToken?> GetValidResetTokenAsync(string tokenHash)
    {
        try

        {  
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Auth_GetValidPasswordResetToken");
        command.Parameters.AddWithValue("@TokenHash", tokenHash);

        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new PasswordResetToken
        {
            PasswordResetTokenId = reader.GetInt32(reader.GetOrdinal("PasswordResetTokenId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId"))
        };
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task UpdatePasswordAsync(int userId, string passwordHash, string passwordSalt, int iterations, string tokenHash)
    {
        try
        {  
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Auth_ResetPassword");
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@PasswordHash", passwordHash);
        command.Parameters.AddWithValue("@PasswordSalt", passwordSalt);
        command.Parameters.AddWithValue("@PasswordIterations", iterations);
        command.Parameters.AddWithValue("@TokenHash", tokenHash);

        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<List<GroupResponse>> GetGroupsAsync(bool includeCustomer)
    {
        try
        {  
        var groups = new List<GroupResponse>();
        await using var command = datalayer.CreateTextCommand("""
            SELECT
                GroupId,
                GroupName,
                Description
            FROM dbo.Groups
            WHERE IsActive = 1
              AND (@IncludeCustomer = 1 OR GroupName <> 'Customer')
            ORDER BY GroupName;
            """);
        command.Parameters.AddWithValue("@IncludeCustomer", includeCustomer);

        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            groups.Add(new GroupResponse
            {
                GroupId = reader.GetInt32(reader.GetOrdinal("GroupId")),
                GroupName = reader.GetString(reader.GetOrdinal("GroupName")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description"))
            });
        }

        return groups;

        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<List<ManagedUserResponse>> GetUsersAsync()
    {
        try
        {  
        var users = new List<ManagedUserResponse>();
        await using var command = datalayer.CreateTextCommand("""
            SELECT
                users.UserId,
                users.Username,
                users.DisplayName,
                users.EmailAddress,
                users.IsActive,
                STRING_AGG(groups.GroupName, ', ') WITHIN GROUP (ORDER BY groups.GroupName) AS Groups,
                users.HasAllRegions,
                regions.RegionNames
            FROM dbo.Users users
            LEFT JOIN dbo.UserGroups userGroups ON userGroups.UserId = users.UserId
            LEFT JOIN dbo.Groups groups ON groups.GroupId = userGroups.GroupId AND groups.IsActive = 1
            OUTER APPLY
            (
                SELECT STRING_AGG(userRegions.RegionName, ', ') WITHIN GROUP (ORDER BY userRegions.RegionName) AS RegionNames
                FROM dbo.UserRegions userRegions
                WHERE userRegions.UserId = users.UserId
            ) regions
            GROUP BY
                users.UserId,
                users.Username,
                users.DisplayName,
                users.EmailAddress,
                users.IsActive,
                users.HasAllRegions,
                regions.RegionNames
            ORDER BY users.DisplayName;
            """);

        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(new ManagedUserResponse
            {
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                Username = reader.GetString(reader.GetOrdinal("Username")),
                DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
                EmailAddress = reader.IsDBNull(reader.GetOrdinal("EmailAddress")) ? null : reader.GetString(reader.GetOrdinal("EmailAddress")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                HasAllRegions = HasColumn(reader, "HasAllRegions") && reader.GetBoolean(reader.GetOrdinal("HasAllRegions")),
                Groups = reader.IsDBNull(reader.GetOrdinal("Groups"))
                    ? []
                    : reader.GetString(reader.GetOrdinal("Groups")).Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList(),
                Regions = HasColumn(reader, "RegionNames") && !reader.IsDBNull(reader.GetOrdinal("RegionNames"))
                    ? reader.GetString(reader.GetOrdinal("RegionNames")).Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList()
                    : []
            });
        }

        return users;
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<ManagedUserResponse> CreateUserAsync(string username, string displayName, string emailAddress, string passwordHash, string passwordSalt, int passwordIterations, List<int> groupIds, bool hasAllRegions, List<string> regions)
    {
         
        var distinctGroupIds = groupIds.Distinct().ToList();
        var distinctRegions = CleanRegions(regions);
        var groupValues = distinctGroupIds.Count == 0
            ? "SELECT CAST(NULL AS INT) AS GroupId WHERE 1 = 0"
            : string.Join(" UNION ALL ", distinctGroupIds.Select((_, index) => $"SELECT @GroupId{index} AS GroupId"));
        var regionValues = distinctRegions.Count == 0
            ? "SELECT CAST(NULL AS NVARCHAR(120)) AS RegionName WHERE 1 = 0"
            : string.Join(" UNION ALL ", distinctRegions.Select((_, index) => $"SELECT @RegionName{index} AS RegionName"));

        await using var command = datalayer.CreateTextCommand($"""
            DECLARE @CreatedUser TABLE
            (
                UserId INT NOT NULL
            );

            INSERT INTO dbo.Users
            (
                Username,
                DisplayName,
                EmailAddress,
                PasswordHash,
                PasswordSalt,
                PasswordIterations,
                HasAllRegions,
                IsActive,
                CreatedDate
            )
            OUTPUT INSERTED.UserId INTO @CreatedUser(UserId)
            VALUES
            (
                @Username,
                @DisplayName,
                @EmailAddress,
                @PasswordHash,
                @PasswordSalt,
                @PasswordIterations,
                @HasAllRegions,
                1,
                SYSUTCDATETIME()
            );

            INSERT INTO dbo.UserGroups (UserId, GroupId)
            SELECT created.UserId, groupIds.GroupId
            FROM @CreatedUser created
            CROSS JOIN ({groupValues}) groupIds
            INNER JOIN dbo.Groups groups ON groups.GroupId = groupIds.GroupId AND groups.IsActive = 1;

            INSERT INTO dbo.UserRegions (UserId, RegionName)
            SELECT created.UserId, regionValues.RegionName
            FROM @CreatedUser created
            CROSS JOIN ({regionValues}) regionValues
            WHERE @HasAllRegions = 0
              AND regionValues.RegionName IS NOT NULL;

            SELECT
                users.UserId,
                users.Username,
                users.DisplayName,
                users.EmailAddress,
                users.IsActive,
                STRING_AGG(groups.GroupName, ', ') WITHIN GROUP (ORDER BY groups.GroupName) AS Groups,
                users.HasAllRegions,
                regions.RegionNames
            FROM dbo.Users users
            INNER JOIN @CreatedUser created ON created.UserId = users.UserId
            LEFT JOIN dbo.UserGroups userGroups ON userGroups.UserId = users.UserId
            LEFT JOIN dbo.Groups groups ON groups.GroupId = userGroups.GroupId AND groups.IsActive = 1
            OUTER APPLY
            (
                SELECT STRING_AGG(userRegions.RegionName, ', ') WITHIN GROUP (ORDER BY userRegions.RegionName) AS RegionNames
                FROM dbo.UserRegions userRegions
                WHERE userRegions.UserId = users.UserId
            ) regions
            GROUP BY
                users.UserId,
                users.Username,
                users.DisplayName,
                users.EmailAddress,
                users.IsActive,
                users.HasAllRegions,
                regions.RegionNames;
            """);
        command.Parameters.AddWithValue("@Username", username);
        command.Parameters.AddWithValue("@DisplayName", displayName);
        command.Parameters.AddWithValue("@EmailAddress", emailAddress);
        command.Parameters.AddWithValue("@PasswordHash", passwordHash);
        command.Parameters.AddWithValue("@PasswordSalt", passwordSalt);
        command.Parameters.AddWithValue("@PasswordIterations", passwordIterations);
        command.Parameters.AddWithValue("@HasAllRegions", hasAllRegions);
        for (var i = 0; i < distinctGroupIds.Count; i++)
        {
            command.Parameters.AddWithValue($"@GroupId{i}", distinctGroupIds[i]);
        }
        for (var i = 0; i < distinctRegions.Count; i++)
        {
            command.Parameters.AddWithValue($"@RegionName{i}", distinctRegions[i]);
        }

        try
        {
            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("The user could not be created.");
            }

            return new ManagedUserResponse
            {
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                Username = reader.GetString(reader.GetOrdinal("Username")),
                DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
                EmailAddress = reader.IsDBNull(reader.GetOrdinal("EmailAddress")) ? null : reader.GetString(reader.GetOrdinal("EmailAddress")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                HasAllRegions = HasColumn(reader, "HasAllRegions") && reader.GetBoolean(reader.GetOrdinal("HasAllRegions")),
                Groups = reader.IsDBNull(reader.GetOrdinal("Groups"))
                    ? []
                    : reader.GetString(reader.GetOrdinal("Groups")).Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList(),
                Regions = HasColumn(reader, "RegionNames") && !reader.IsDBNull(reader.GetOrdinal("RegionNames"))
                    ? reader.GetString(reader.GetOrdinal("RegionNames")).Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList()
                    : []
            };
        }
        catch (SqlException error) when (error.Number is 2601 or 2627)
        {
            throw new InvalidOperationException("A user with this email address already exists.", error);
        }
        catch (SqlException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception error)
        {
            throw new InvalidOperationException("The user could not be created.", error);
        }
    }

    public async Task<ManagedUserResponse> UpdateUserAsync(int userId, string username, string displayName, string emailAddress, bool isActive, List<int> groupIds, bool hasAllRegions, List<string> regions)
    { 
        try
        {  
        var distinctGroupIds = groupIds.Distinct().ToList();
        var distinctRegions = CleanRegions(regions);
        var groupValues = distinctGroupIds.Count == 0
            ? "SELECT CAST(NULL AS INT) AS GroupId WHERE 1 = 0"
            : string.Join(" UNION ALL ", distinctGroupIds.Select((_, index) => $"SELECT @GroupId{index} AS GroupId"));
        var regionValues = distinctRegions.Count == 0
            ? "SELECT CAST(NULL AS NVARCHAR(120)) AS RegionName WHERE 1 = 0"
            : string.Join(" UNION ALL ", distinctRegions.Select((_, index) => $"SELECT @RegionName{index} AS RegionName"));

        await using var command = datalayer.CreateTextCommand($"""
            UPDATE dbo.Users
            SET
                Username = @Username,
                DisplayName = @DisplayName,
                EmailAddress = @EmailAddress,
                IsActive = @IsActive,
                HasAllRegions = @HasAllRegions,
                ModifiedDate = SYSUTCDATETIME()
            WHERE UserId = @UserId;

            IF @@ROWCOUNT = 0
            BEGIN
                SELECT CAST(0 AS BIT) AS UserExists;
                RETURN;
            END;

            DELETE FROM dbo.UserGroups
            WHERE UserId = @UserId;

            INSERT INTO dbo.UserGroups (UserId, GroupId)
            SELECT @UserId, groupIds.GroupId
            FROM ({groupValues}) groupIds
            INNER JOIN dbo.Groups groups ON groups.GroupId = groupIds.GroupId AND groups.IsActive = 1;

            DELETE FROM dbo.UserRegions
            WHERE UserId = @UserId;

            INSERT INTO dbo.UserRegions (UserId, RegionName)
            SELECT @UserId, regionValues.RegionName
            FROM ({regionValues}) regionValues
            WHERE @HasAllRegions = 0
              AND regionValues.RegionName IS NOT NULL;

            SELECT
                CAST(1 AS BIT) AS UserExists,
                users.UserId,
                users.Username,
                users.DisplayName,
                users.EmailAddress,
                users.IsActive,
                STRING_AGG(groups.GroupName, ', ') WITHIN GROUP (ORDER BY groups.GroupName) AS Groups,
                users.HasAllRegions,
                regions.RegionNames
            FROM dbo.Users users
            LEFT JOIN dbo.UserGroups userGroups ON userGroups.UserId = users.UserId
            LEFT JOIN dbo.Groups groups ON groups.GroupId = userGroups.GroupId AND groups.IsActive = 1
            OUTER APPLY
            (
                SELECT STRING_AGG(userRegions.RegionName, ', ') WITHIN GROUP (ORDER BY userRegions.RegionName) AS RegionNames
                FROM dbo.UserRegions userRegions
                WHERE userRegions.UserId = users.UserId
            ) regions
            WHERE users.UserId = @UserId
            GROUP BY
                users.UserId,
                users.Username,
                users.DisplayName,
                users.EmailAddress,
                users.IsActive,
                users.HasAllRegions,
                regions.RegionNames;
            """);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Username", username);
        command.Parameters.AddWithValue("@DisplayName", displayName);
        command.Parameters.AddWithValue("@EmailAddress", emailAddress);
        command.Parameters.AddWithValue("@IsActive", isActive);
        command.Parameters.AddWithValue("@HasAllRegions", hasAllRegions);
        for (var i = 0; i < distinctGroupIds.Count; i++)
        {
            command.Parameters.AddWithValue($"@GroupId{i}", distinctGroupIds[i]);
        }
        for (var i = 0; i < distinctRegions.Count; i++)
        {
            command.Parameters.AddWithValue($"@RegionName{i}", distinctRegions[i]);
        }

        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || !reader.GetBoolean(reader.GetOrdinal("UserExists")))
        {
            throw new KeyNotFoundException("The selected user could not be found.");
        }

        return new ManagedUserResponse
        {
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            Username = reader.GetString(reader.GetOrdinal("Username")),
            DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
            EmailAddress = reader.IsDBNull(reader.GetOrdinal("EmailAddress")) ? null : reader.GetString(reader.GetOrdinal("EmailAddress")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            HasAllRegions = HasColumn(reader, "HasAllRegions") && reader.GetBoolean(reader.GetOrdinal("HasAllRegions")),
            Groups = reader.IsDBNull(reader.GetOrdinal("Groups"))
                ? []
                : reader.GetString(reader.GetOrdinal("Groups")).Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList(),
            Regions = HasColumn(reader, "RegionNames") && !reader.IsDBNull(reader.GetOrdinal("RegionNames"))
                ? reader.GetString(reader.GetOrdinal("RegionNames")).Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList()
                : []
        };
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<ManagedUserResponse> ResetUserPasswordAsync(int userId, string passwordHash, string passwordSalt, int passwordIterations)
    {
        await using var command = datalayer.CreateTextCommand("""
            UPDATE dbo.Users
            SET
                PasswordHash = @PasswordHash,
                PasswordSalt = @PasswordSalt,
                PasswordIterations = @PasswordIterations,
                ModifiedDate = SYSUTCDATETIME()
            WHERE UserId = @UserId;

            IF @@ROWCOUNT = 0
            BEGIN
                SELECT CAST(0 AS BIT) AS UserExists;
                RETURN;
            END;

            SELECT
                CAST(1 AS BIT) AS UserExists,
                users.UserId,
                users.Username,
                users.DisplayName,
                users.EmailAddress,
                users.IsActive,
                STRING_AGG(groups.GroupName, ', ') WITHIN GROUP (ORDER BY groups.GroupName) AS Groups
            FROM dbo.Users users
            LEFT JOIN dbo.UserGroups userGroups ON userGroups.UserId = users.UserId
            LEFT JOIN dbo.Groups groups ON groups.GroupId = userGroups.GroupId AND groups.IsActive = 1
            WHERE users.UserId = @UserId
            GROUP BY
                users.UserId,
                users.Username,
                users.DisplayName,
                users.EmailAddress,
                users.IsActive;
            """);

        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@PasswordHash", passwordHash);
        command.Parameters.AddWithValue("@PasswordSalt", passwordSalt);
        command.Parameters.AddWithValue("@PasswordIterations", passwordIterations);

        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || !reader.GetBoolean(reader.GetOrdinal("UserExists")))
        {
            throw new KeyNotFoundException("The selected user could not be found.");
        }

        return new ManagedUserResponse
        {
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            Username = reader.GetString(reader.GetOrdinal("Username")),
            DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
            EmailAddress = reader.IsDBNull(reader.GetOrdinal("EmailAddress")) ? null : reader.GetString(reader.GetOrdinal("EmailAddress")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            HasAllRegions = HasColumn(reader, "HasAllRegions") ? reader.GetBoolean(reader.GetOrdinal("HasAllRegions")) : true,
            Groups = reader.IsDBNull(reader.GetOrdinal("Groups"))
                ? []
                : reader.GetString(reader.GetOrdinal("Groups")).Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList(),
            Regions = HasColumn(reader, "RegionNames") && !reader.IsDBNull(reader.GetOrdinal("RegionNames"))
                ? reader.GetString(reader.GetOrdinal("RegionNames")).Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList()
                : []
        };
    }

    private static List<string> CleanRegions(IEnumerable<string>? regions)
    {
        return (regions ?? [])
            .Select(region => region.Trim())
            .Where(region => !string.IsNullOrWhiteSpace(region))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(region => region)
            .ToList();
    }

    private static bool HasColumn(SqlDataReader reader, string columnName)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
