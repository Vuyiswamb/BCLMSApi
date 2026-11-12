using BCLMSApi.Models;

namespace BCLMSApi.Data;

public sealed class ReceptionVisitRepository(Datalayer data)
{
    public async Task<List<ReceptionVisitResponse>> ListAsync()
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_ReceptionVisits_GetAll");
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        var visits = new List<ReceptionVisitResponse>();
        while (await reader.ReadAsync())
        {
            visits.Add(new ReceptionVisitResponse
            {
                ReceptionVisitId = reader.GetInt32(0), VisitDate = reader.GetDateTime(1), FullName = reader.GetString(2),
                ContactNumber = reader.GetString(3), ServiceRequired = reader.GetString(4), Referral = reader.IsDBNull(5) ? null : reader.GetString(5),
                CapturedBy = reader.GetString(6), CreatedDate = reader.GetDateTime(7),
            });
        }
        return visits;
    }

    public async Task<ReceptionVisitResponse> CreateAsync(ReceptionVisitCreateRequest request, int userId)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_ReceptionVisits_Create");
        command.Parameters.AddWithValue("@VisitDate", request.VisitDate.Date);
        command.Parameters.AddWithValue("@FullName", request.FullName.Trim());
        command.Parameters.AddWithValue("@ContactNumber", request.ContactNumber.Trim());
        command.Parameters.AddWithValue("@ServiceRequired", request.ServiceRequired.Trim());
        command.Parameters.AddWithValue("@Referral", string.IsNullOrWhiteSpace(request.Referral) ? DBNull.Value : request.Referral.Trim());
        command.Parameters.AddWithValue("@CapturedByUserId", userId);
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("The reception visit could not be saved.");
        return new ReceptionVisitResponse
        {
            ReceptionVisitId = reader.GetInt32(0), VisitDate = reader.GetDateTime(1), FullName = reader.GetString(2), ContactNumber = reader.GetString(3),
            ServiceRequired = reader.GetString(4), Referral = reader.IsDBNull(5) ? null : reader.GetString(5), CapturedBy = reader.GetString(6), CreatedDate = reader.GetDateTime(7),
        };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_ReceptionVisits_Delete");
        command.Parameters.AddWithValue("@ReceptionVisitId", id);
        await command.Connection!.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return result is not null && Convert.ToInt32(result) > 0;
    }
}
