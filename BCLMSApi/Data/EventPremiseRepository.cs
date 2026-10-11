using BCLMSApi.Models;

namespace BCLMSApi.Data;

public class EventPremiseRepository(Datalayer datalayer)
{
    public async Task<List<EventPremiseResponse>> GetAllAsync()
    {
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_EventPremises_GetAll");
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        var premises = new List<EventPremiseResponse>();
        while (await reader.ReadAsync())
        {
            premises.Add(Map(reader));
        }

        return premises;
    }

    public async Task<EventPremiseResponse?> SaveAsync(
        int? id,
        EventPremiseSaveRequest request,
        int userId)
    {
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_EventPremises_Save");
        command.Parameters.AddWithValue("@EventPremiseId", (object?)id ?? DBNull.Value);
        command.Parameters.AddWithValue("@Name", request.Name.Trim());
        command.Parameters.AddWithValue("@StreetNameAndNumber", request.StreetNameAndNumber.Trim());
        command.Parameters.AddWithValue("@ContactDetails", request.ContactDetails.Trim());
        command.Parameters.AddWithValue("@Latitude", request.Latitude);
        command.Parameters.AddWithValue("@Longitude", request.Longitude);
        command.Parameters.AddWithValue("@UserId", userId);
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task<bool> DisableAsync(int id, int userId)
    {
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_EventPremises_Disable");
        command.Parameters.AddWithValue("@EventPremiseId", id);
        command.Parameters.AddWithValue("@UserId", userId);
        await command.Connection!.OpenAsync();
        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }

    private static EventPremiseResponse Map(System.Data.IDataRecord reader)
    {
        return new EventPremiseResponse
        {
            EventPremiseId = reader.GetInt32(0),
            Name = reader.GetString(1),
            StreetNameAndNumber = reader.GetString(2),
            ContactDetails = reader.GetString(3),
            Latitude = reader.GetDecimal(4),
            Longitude = reader.GetDecimal(5),
            IsActive = reader.GetBoolean(6)
        };
    }
}
