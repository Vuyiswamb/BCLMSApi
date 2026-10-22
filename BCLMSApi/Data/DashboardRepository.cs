namespace BCLMSApi.Data;

public sealed record DashboardCustomerRow(
    int UserId,
    string? BusinessName,
    int? TownshipId,
    string? RegionName,
    double? Latitude,
    double? Longitude);

public sealed record DashboardRegistrationRow(int Year, int Month, int Count);

public class DashboardRepository(Datalayer datalayer)
{
    public async Task<List<DashboardCustomerRow>> GetCustomersAsync(CancellationToken cancellationToken)
    {
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Dashboard_GetCustomers");
        await command.Connection!.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var customers = new List<DashboardCustomerRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            customers.Add(new DashboardCustomerRow(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : Convert.ToDouble(reader.GetValue(4)),
                reader.IsDBNull(5) ? null : Convert.ToDouble(reader.GetValue(5))));
        }

        return customers;
    }

    public async Task<List<DashboardRegistrationRow>> GetCustomerRegistrationCountsAsync(CancellationToken cancellationToken)
    {
        // Keep the dashboard available while older environments are waiting for
        // the accompanying stored-procedure deployment.
        await using var command = datalayer.CreateTextCommand("""
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
            """);
        await command.Connection!.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var registrations = new List<DashboardRegistrationRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            registrations.Add(new DashboardRegistrationRow(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)));
        }

        return registrations;
    }
}
