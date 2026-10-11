using BCLMSApi.Models;

namespace BCLMSApi.Data;

public sealed record AssistedIntakeRow(
    int IntakeId,
    int CustomerUserId,
    string FullName,
    string Email,
    string IdNumber,
    string Phone,
    int? BusinessId,
    int? ApplicationId,
    string? Response);

public sealed record AssistedIntakeListRow(
    int IntakeId,
    string FullName,
    string Email,
    int? ApplicationId,
    string? TrackingNumber);

public class AssistedIntakeRepository(Datalayer data)
{
    public async Task<List<AssistedIntakeListRow>> ListAsync(int staffId)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_AssistedIntakes_List");
        command.Parameters.AddWithValue("@StaffId", staffId);
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        var intakes = new List<AssistedIntakeListRow>();
        while (await reader.ReadAsync())
        {
            intakes.Add(new AssistedIntakeListRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return intakes;
    }

    public async Task<int?> FindCustomerAsync(string email)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_AssistedIntakes_FindCustomer");
        command.Parameters.AddWithValue("@Email", email.Trim());
        await command.Connection!.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return result is null ? null : Convert.ToInt32(result);
    }

    public async Task<int> CreateAsync(int staffId, int customerId, string idNumber, string phone)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_AssistedIntakes_Create");
        command.Parameters.AddWithValue("@StaffId", staffId);
        command.Parameters.AddWithValue("@CustomerId", customerId);
        command.Parameters.AddWithValue("@IdNumber", idNumber.Trim());
        command.Parameters.AddWithValue("@Phone", phone.Trim());
        await command.Connection!.OpenAsync();
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<AssistedIntakeRow> GetAsync(int intakeId, int staffId)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_AssistedIntakes_Get");
        command.Parameters.AddWithValue("@IntakeId", intakeId);
        command.Parameters.AddWithValue("@StaffId", staffId);
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new KeyNotFoundException("Assisted intake not found for your account.");
        }

        return new AssistedIntakeRow(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetInt32(6),
            reader.IsDBNull(7) ? null : reader.GetInt32(7),
            reader.IsDBNull(8) ? null : reader.GetString(8));
    }

    public async Task LinkBusinessAsync(int intakeId, int businessId)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_AssistedIntakes_LinkBusiness");
        command.Parameters.AddWithValue("@IntakeId", intakeId);
        command.Parameters.AddWithValue("@BusinessId", businessId);
        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    public async Task RecordWorkshopAsync(int intakeId, int customerId, int businessId)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_AssistedIntakes_RecordWorkshop");
        command.Parameters.AddWithValue("@IntakeId", intakeId);
        command.Parameters.AddWithValue("@CustomerId", customerId);
        command.Parameters.AddWithValue("@BusinessId", businessId);
        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveApplicationAsync(int intakeId, int applicationId, string response)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_AssistedIntakes_SaveApplication");
        command.Parameters.AddWithValue("@IntakeId", intakeId);
        command.Parameters.AddWithValue("@ApplicationId", applicationId);
        command.Parameters.AddWithValue("@Response", response);
        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }
}
