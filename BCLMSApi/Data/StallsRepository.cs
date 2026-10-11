using System.Data;
using BCLMSApi.Models;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Data;

public class StallsRepository(Datalayer data)
{
    public async Task<List<StallResponse>> GetAllAsync()
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_Stalls_GetAll");
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        var stalls = new List<StallResponse>();
        while (await reader.ReadAsync())
        {
            stalls.Add(MapStall(reader));
        }

        return stalls;
    }

    public async Task<List<StallApplicationOptionResponse>> GetApplicationOptionsAsync()
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_Stalls_GetApplicationOptions");
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        var applications = new List<StallApplicationOptionResponse>();
        while (await reader.ReadAsync())
        {
            applications.Add(new StallApplicationOptionResponse
            {
                ApplicationId = reader.GetInt32(reader.GetOrdinal("ApplicationId")),
                TrackingNumber = reader.GetString(reader.GetOrdinal("TrackingNumber")),
                BusinessName = reader.GetString(reader.GetOrdinal("BusinessName")),
                ApplicantName = reader.GetString(reader.GetOrdinal("ApplicantName")),
                LicenceType = reader.GetString(reader.GetOrdinal("LicenceType")),
                CurrentStage = reader.GetString(reader.GetOrdinal("CurrentStage")),
                Status = reader.GetString(reader.GetOrdinal("Status"))
            });
        }

        return applications;
    }

    public async Task<List<RestrictedTradingAreaResponse>> GetAreasAsync()
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_RestrictedTradingAreas_GetAll");
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        var areas = new List<RestrictedTradingAreaResponse>();
        while (await reader.ReadAsync())
        {
            areas.Add(MapArea(reader));
        }

        return areas;
    }

    public async Task<RestrictedTradingAreaResponse?> SaveAreaAsync(
        int? id,
        RestrictedTradingAreaSaveRequest request,
        int userId)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_RestrictedTradingAreas_Save");
        AddAreaParameters(command, request);
        command.Parameters.AddWithValue("@RestrictedAreaId", (object?)id ?? DBNull.Value);
        command.Parameters.AddWithValue("@UserId", userId);

        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapArea(reader) : null;
    }

    public async Task ArchiveAreaAsync(int id, int userId)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_RestrictedTradingAreas_Archive");
        command.Parameters.AddWithValue("@RestrictedAreaId", id);
        command.Parameters.AddWithValue("@UserId", userId);
        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    public async Task<StallResponse?> SaveAsync(int? id, StallSaveRequest request, int userId)
    {
        var procedureName = id is null ? "dbo.usp_Stalls_Create" : "dbo.usp_Stalls_Update";
        await using var command = data.CreateStoredProcedureCommand(procedureName);

        if (id is not null)
        {
            command.Parameters.AddWithValue("@StallId", id);
        }

        command.Parameters.AddWithValue("@StallName", string.IsNullOrWhiteSpace(request.StallName) ? DBNull.Value : request.StallName.Trim());
        command.Parameters.AddWithValue("@StallType", request.StallType.Trim());
        command.Parameters.AddWithValue("@Address", request.Address.Trim());
        command.Parameters.AddWithValue("@AreaName", string.IsNullOrWhiteSpace(request.AreaName) ? DBNull.Value : request.AreaName.Trim());
        command.Parameters.AddWithValue("@Latitude", request.Latitude);
        command.Parameters.AddWithValue("@Longitude", request.Longitude);
        command.Parameters.AddWithValue(id is null ? "@CreatedByUserId" : "@UpdatedByUserId", userId);

        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapStall(reader) : null;
    }

    public async Task ArchiveAsync(int id, int userId)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_Stalls_Archive");
        command.Parameters.AddWithValue("@StallId", id);
        command.Parameters.AddWithValue("@ArchivedByUserId", userId);
        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    public async Task<StallAllocationResponse?> AllocateAsync(
        StallAllocationRequest request,
        int userId,
        string displayName)
    {
        await using var command = data.CreateStoredProcedureCommand("dbo.usp_Stalls_AllocateToApplication");
        command.Parameters.AddWithValue("@ApplicationId", request.ApplicationId);
        command.Parameters.AddWithValue("@StallId", request.StallId);
        command.Parameters.AddWithValue("@AllocatedByUserId", userId);
        command.Parameters.AddWithValue("@AllocatedByDisplayName", displayName);

        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new StallAllocationResponse
        {
            ApplicationStallAllocationId = reader.GetInt32(reader.GetOrdinal("ApplicationStallAllocationId")),
            ApplicationId = reader.GetInt32(reader.GetOrdinal("ApplicationId")),
            StallId = reader.GetInt32(reader.GetOrdinal("StallId")),
            StallNumber = reader.GetString(reader.GetOrdinal("StallNumber")),
            StallName = reader.GetString(reader.GetOrdinal("StallName")),
            IsRestrictedArea = reader.GetBoolean(reader.GetOrdinal("IsRestrictedArea")),
            InspectionGroupName = reader.GetString(reader.GetOrdinal("InspectionGroupName"))
        };
    }

    private static void AddAreaParameters(SqlCommand command, RestrictedTradingAreaSaveRequest request)
    {
        command.Parameters.AddWithValue("@StreetName", request.StreetName.Trim());
        command.Parameters.AddWithValue("@AreaCode", request.AreaCode.Trim());
        command.Parameters.AddWithValue("@Category", string.IsNullOrWhiteSpace(request.Category) ? DBNull.Value : request.Category.Trim());
        command.Parameters.AddWithValue("@DisplayName", string.IsNullOrWhiteSpace(request.DisplayName) ? DBNull.Value : request.DisplayName.Trim());
        command.Parameters.AddWithValue("@CenterLatitude", request.CenterLatitude is null ? DBNull.Value : request.CenterLatitude);
        command.Parameters.AddWithValue("@CenterLongitude", request.CenterLongitude is null ? DBNull.Value : request.CenterLongitude);
        command.Parameters.AddWithValue("@MapZoom", request.MapZoom is null ? DBNull.Value : request.MapZoom);
        command.Parameters.AddWithValue("@GeometryType", string.IsNullOrWhiteSpace(request.GeometryType) ? DBNull.Value : request.GeometryType.Trim());
        command.Parameters.AddWithValue("@GeometryJson", string.IsNullOrWhiteSpace(request.GeometryJson) ? DBNull.Value : request.GeometryJson.Trim());
    }

    private static StallResponse MapStall(IDataRecord reader)
    {
        return new StallResponse
        {
            StallId = reader.GetInt32(reader.GetOrdinal("StallId")),
            StallNumber = reader.GetString(reader.GetOrdinal("StallNumber")),
            StallName = reader.GetString(reader.GetOrdinal("StallName")),
            StallType = reader.GetString(reader.GetOrdinal("StallType")),
            Address = reader.GetString(reader.GetOrdinal("Address")),
            AreaName = NullableString(reader, "AreaName"),
            Latitude = reader.GetDecimal(reader.GetOrdinal("Latitude")),
            Longitude = reader.GetDecimal(reader.GetOrdinal("Longitude")),
            IsRestrictedArea = reader.GetBoolean(reader.GetOrdinal("IsRestrictedArea")),
            RestrictedStreetName = NullableString(reader, "RestrictedStreetName"),
            RestrictedAreaCode = NullableString(reader, "RestrictedAreaCode"),
            IsOccupied = reader.GetBoolean(reader.GetOrdinal("IsOccupied")),
            AllocatedTrackingNumber = NullableString(reader, "AllocatedTrackingNumber"),
            AllocatedBusinessName = NullableString(reader, "AllocatedBusinessName"),
            AllocatedApplicationStatus = NullableString(reader, "AllocatedApplicationStatus"),
            AllocatedApplicationStage = NullableString(reader, "AllocatedApplicationStage")
        };
    }

    private static RestrictedTradingAreaResponse MapArea(IDataRecord reader)
    {
        return new RestrictedTradingAreaResponse
        {
            RestrictedAreaId = reader.GetInt32(reader.GetOrdinal("RestrictedAreaId")),
            StreetName = reader.GetString(reader.GetOrdinal("StreetName")),
            AreaCode = reader.GetString(reader.GetOrdinal("AreaCode")),
            Category = NullableString(reader, "Category"),
            DisplayName = NullableString(reader, "DisplayName"),
            CenterLatitude = NullableDecimal(reader, "CenterLatitude"),
            CenterLongitude = NullableDecimal(reader, "CenterLongitude"),
            MapZoom = NullableInt(reader, "MapZoom"),
            GeometryType = NullableString(reader, "GeometryType"),
            GeometryJson = NullableString(reader, "GeometryJson"),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
        };
    }

    private static string? NullableString(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static decimal? NullableDecimal(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static int? NullableInt(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }
}
