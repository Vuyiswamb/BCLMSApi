using System.Data;
using BCLMSApi.Models;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Data;

public sealed class WorkshopRepositoryException(int number, string message) : Exception(message)
{
    public int Number
    {
        get;
    } = number;
}

public class WorkshopRepository(Datalayer data)
{
    public Task<List<WorkshopAttendanceRequestResponse>> GetInternalAsync()
    {
        return ReadManyAsync("dbo.usp_WorkshopAttendanceRequests_GetInternal");
    }

    public Task<List<WorkshopAttendanceRequestResponse>> GetMineAsync(int userId)
    {
        return ReadManyAsync("dbo.usp_WorkshopAttendanceRequests_GetMine", command =>
            command.Parameters.AddWithValue("@UserId", userId));
    }

    public async Task<WorkshopAttendanceRequestResponse?> GetByIdAsync(int requestId)
    {
        var rows = await ReadManyAsync("dbo.usp_WorkshopAttendanceRequests_GetById", command =>
            command.Parameters.AddWithValue("@RequestId", requestId));

        return rows.FirstOrDefault();
    }

    public async Task<int> CreateAsync(WorkshopAttendanceRequestCreateRequest request, int userId)
    {
        try
        {
            await using var command = data.CreateStoredProcedureCommand("dbo.usp_WorkshopAttendanceRequests_Create");
            command.Parameters.AddWithValue("@BusinessId", request.BusinessId);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@RequestedLicenceType", request.RequestedLicenceType.Trim());
            command.Parameters.AddWithValue("@WorkshopMode", request.WorkshopMode);
            command.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(request.Notes) ? DBNull.Value : request.Notes.Trim());
            await command.Connection!.OpenAsync();
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        catch (SqlException error) when (error.Number is 51001 or 51002 or 51003)
        {
            throw new WorkshopRepositoryException(error.Number, error.Message);
        }
    }

    public async Task<bool> MarkAttendedAsync(int requestId)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_WorkshopAttendanceRequests_MarkAttended");
        command.Parameters.AddWithValue("@RequestId", requestId);
        await command.Connection!.OpenAsync();
        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }

    public async Task<bool> ScheduleAsync(int requestId, DateTime date)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_WorkshopAttendanceRequests_Schedule");
        command.Parameters.AddWithValue("@RequestId", requestId);
        command.Parameters.AddWithValue("@ScheduledWorkshopDate", date.Date);
        await command.Connection!.OpenAsync();
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    public async Task MarkInvitedAsync(int requestId)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_WorkshopAttendanceRequests_MarkInvited");
        command.Parameters.AddWithValue("@RequestId", requestId);
        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<WorkshopAttendanceRequestResponse>> ReadManyAsync(string procedureName, Action<SqlCommand>? bind = null)
    {
        await using var command = data.CreateStoredProcedureCommand(procedureName);
        bind?.Invoke(command);
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        var requests = new List<WorkshopAttendanceRequestResponse>();
        while (await reader.ReadAsync())
        {
            requests.Add(Map(reader));
        }
        return requests;
    }

    private static WorkshopAttendanceRequestResponse Map(IDataRecord reader)
    {
        return new WorkshopAttendanceRequestResponse
        {
            WorkshopAttendanceRequestId = reader.GetInt32(reader.GetOrdinal("WorkshopAttendanceRequestId")),
            BusinessId = reader.GetInt32(reader.GetOrdinal("BusinessId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            RequestedLicenceType = reader.GetString(reader.GetOrdinal("RequestedLicenceType")),
            WorkshopMode = NullableString(reader, "WorkshopMode"),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            RequestedDate = reader.GetDateTime(reader.GetOrdinal("RequestedDate")),
            ScheduledWorkshopDate = NullableDate(reader, "ScheduledWorkshopDate"),
            InvitedDate = NullableDate(reader, "InvitedDate"),
            AttendedDate = NullableDate(reader, "AttendedDate"),
            Notes = NullableString(reader, "Notes"),
            BusinessName = reader.GetString(reader.GetOrdinal("BusinessName")),
            OwnerName = reader.GetString(reader.GetOrdinal("OwnerName")),
            EmailAddress = reader.GetString(reader.GetOrdinal("EmailAddress")),
            RegionName = NullableString(reader, "RegionName"),
            MobileNumber = reader.GetString(reader.GetOrdinal("MobileNumber")),
            TownshipName = NullableString(reader, "TownshipName")
        };
    }

    private static string? NullableString(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTime? NullableDate(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }
}
