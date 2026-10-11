using System.Data;

namespace BCLMSApi.Data;

public sealed record PermitStallData(
    object? StallNumber,
    object? StallName,
    object? Address,
    object? Latitude,
    object? Longitude);

public sealed record PermitPhotoData(string ContentType, byte[]? Content);

public class PermitVerificationRepository(Datalayer data)
{
    public async Task<List<PermitStallData>> GetStallsAsync(string trackingNumber)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_Stalls_GetAll");
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        var stalls = new List<PermitStallData>();
        while (await reader.ReadAsync())
        {
            if (string.Equals(
                    reader["AllocatedTrackingNumber"] as string,
                    trackingNumber,
                    StringComparison.OrdinalIgnoreCase))
            {
                stalls.Add(new PermitStallData(
                    reader["StallNumber"],
                    reader["StallName"],
                    reader["Address"],
                    reader["Latitude"],
                    reader["Longitude"]));
            }
        }

        return stalls;
    }

    public async Task SavePhotoAsync(
        int applicationId,
        int userId,
        string contentType,
        byte[] content)
    {
        await using var command = data.CreateStoredProcedureCommand(
            "dbo.usp_PermitHolderPhotos_Save");

        command.Parameters.AddWithValue("@ApplicationId", applicationId);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@ContentType", contentType);
        command.Parameters.Add("@FileContent", SqlDbType.VarBinary, -1).Value = content;

        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    public async Task<PermitPhotoData?> GetPhotoAsync(int applicationId, bool includeContent)
    {
        await using var command = data.CreateStoredProcedureCommand(
            "dbo.usp_PermitHolderPhotos_Get");

        command.Parameters.AddWithValue("@ApplicationId", applicationId);
        command.Parameters.Add("@IncludeContent", SqlDbType.Bit).Value = includeContent;

        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new PermitPhotoData(
            reader.GetString(0),
            includeContent && !reader.IsDBNull(1) ? reader.GetFieldValue<byte[]>(1) : null);
    }
}
