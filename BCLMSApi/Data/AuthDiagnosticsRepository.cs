namespace BCLMSApi.Data;

public sealed record AuthDiagnosticsData(
    bool GetUserForLoginProcedureExists,
    bool UpdateLastLoginProcedureExists,
    AuthDiagnosticsUser? User);

public sealed record AuthDiagnosticsUser(
    int UserId,
    bool IsActive,
    string? PasswordHash,
    string? PasswordSalt,
    int PasswordIterations,
    int GroupCount);

public class AuthDiagnosticsRepository(Datalayer datalayer)
{
    public async Task<AuthDiagnosticsData> GetAsync(string? username)
    {
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Auth_GetDiagnostics");
        command.Parameters.AddWithValue("@Username", (object?)username?.Trim() ?? DBNull.Value);
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        var procedures = await reader.ReadAsync()
            ? (reader.GetBoolean(0), reader.GetBoolean(1))
            : (false, false);

        AuthDiagnosticsUser? user = null;
        if (await reader.NextResultAsync() && await reader.ReadAsync())
        {
            user = new AuthDiagnosticsUser(
                reader.GetInt32(0),
                reader.GetBoolean(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt32(4),
                reader.GetInt32(5));
        }

        return new AuthDiagnosticsData(procedures.Item1, procedures.Item2, user);
    }
}
