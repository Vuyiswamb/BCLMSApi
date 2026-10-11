namespace BCLMSApi.Data;

public sealed record DashboardCustomerRow(
    int UserId,
    string? BusinessName,
    int? TownshipId,
    string? RegionName,
    double? Latitude,
    double? Longitude);

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
}
