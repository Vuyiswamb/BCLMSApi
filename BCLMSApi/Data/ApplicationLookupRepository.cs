using BCLMSApi.Models;

namespace BCLMSApi.Data;

public sealed class ApplicationLookupRepository(Datalayer data)
{
    public async Task<List<ApplicationLookupResponse>> GetAsync(string? type = null, bool activeOnly = true)
    {
        await using var command = data.CreateTextCommand("""
            SELECT LookupId, LookupType, LookupValue, IsActive
            FROM dbo.ApplicationLookups
            WHERE (@LookupType IS NULL OR LookupType = @LookupType)
              AND (@ActiveOnly = 0 OR IsActive = 1)
            ORDER BY LookupType, SortOrder, LookupValue;
            """);
        command.Parameters.AddWithValue("@LookupType", string.IsNullOrWhiteSpace(type) ? DBNull.Value : type.Trim());
        command.Parameters.AddWithValue("@ActiveOnly", activeOnly);
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<ApplicationLookupResponse>();
        while (await reader.ReadAsync()) result.Add(new ApplicationLookupResponse { LookupId = reader.GetInt32(0), LookupType = reader.GetString(1), LookupValue = reader.GetString(2), IsActive = reader.GetBoolean(3) });
        return result;
    }

    public async Task<ApplicationLookupResponse?> SaveAsync(int? id, ApplicationLookupSaveRequest request)
    {
        await using var command = data.CreateTextCommand("""
            IF @LookupId IS NULL
            BEGIN
                INSERT dbo.ApplicationLookups (LookupType, LookupValue, IsActive)
                OUTPUT inserted.LookupId, inserted.LookupType, inserted.LookupValue, inserted.IsActive
                VALUES (@LookupType, @LookupValue, @IsActive);
            END
            ELSE
            BEGIN
                UPDATE dbo.ApplicationLookups SET LookupType=@LookupType, LookupValue=@LookupValue, IsActive=@IsActive, ModifiedDate=SYSUTCDATETIME()
                OUTPUT inserted.LookupId, inserted.LookupType, inserted.LookupValue, inserted.IsActive
                WHERE LookupId=@LookupId;
            END
            """);
        command.Parameters.AddWithValue("@LookupId", (object?)id ?? DBNull.Value);
        command.Parameters.AddWithValue("@LookupType", request.LookupType.Trim());
        command.Parameters.AddWithValue("@LookupValue", request.LookupValue.Trim());
        command.Parameters.AddWithValue("@IsActive", request.IsActive);
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? new ApplicationLookupResponse { LookupId = reader.GetInt32(0), LookupType = reader.GetString(1), LookupValue = reader.GetString(2), IsActive = reader.GetBoolean(3) } : null;
    }

    public async Task DeleteAsync(int id)
    {
        await using var command = data.CreateTextCommand("DELETE FROM dbo.ApplicationLookups WHERE LookupId=@LookupId;");
        command.Parameters.AddWithValue("@LookupId", id);
        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }
}
