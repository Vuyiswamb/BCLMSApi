using System.Data;
using BCLMSApi.Data;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StallsController(Datalayer datalayer, IUserTokenService userTokenService) : ControllerBase
{
    private static readonly string[] StallManagementGroups = ["Super User", "Admin Officer", "Director", "Functional Head"];

    [HttpGet]
    public async Task<ActionResult<List<StallResponse>>> GetStalls()
    {
        var user = GetAuthorizedUser();
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Stall management access is required." });
        }

        try
        {
            var stalls = new List<StallResponse>();
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Stalls_GetAll");
            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                stalls.Add(MapStall(reader));
            }

            return Ok(stalls);
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    [HttpGet("applications")]
    public async Task<ActionResult<List<StallApplicationOptionResponse>>> GetApplicationsForAllocation()
    {
        var user = GetAuthorizedUser();
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Stall management access is required." });
        }

        try
        {
            var applications = new List<StallApplicationOptionResponse>();
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Stalls_GetApplicationOptions");
            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
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

            return Ok(applications);
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    [HttpGet("restricted-areas")]
    public async Task<ActionResult<List<RestrictedTradingAreaResponse>>> GetRestrictedAreas()
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Sign in to view restricted trading areas." });
        }

        try
        {
            var areas = new List<RestrictedTradingAreaResponse>();
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_RestrictedTradingAreas_GetAll");
            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                areas.Add(MapRestrictedTradingArea(reader));
            }

            return Ok(areas);
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    [HttpPost("restricted-areas")]
    public async Task<ActionResult<RestrictedTradingAreaResponse>> SaveRestrictedArea(RestrictedTradingAreaSaveRequest request)
    {
        var user = GetAuthorizedUser();
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Stall management access is required." });
        }

        if (string.IsNullOrWhiteSpace(request.StreetName) || string.IsNullOrWhiteSpace(request.AreaCode))
        {
            return BadRequest(new { message = "Street name and area code are required." });
        }

        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_RestrictedTradingAreas_Save");
            AddRestrictedAreaParameters(command, request);
            command.Parameters.AddWithValue("@RestrictedAreaId", DBNull.Value);
            command.Parameters.AddWithValue("@UserId", user.UserId);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync()
                ? Ok(MapRestrictedTradingArea(reader))
                : StatusCode(StatusCodes.Status500InternalServerError, new { message = "The restricted area could not be saved." });
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    [HttpPut("restricted-areas/{restrictedAreaId:int}")]
    public async Task<ActionResult<RestrictedTradingAreaResponse>> UpdateRestrictedArea(int restrictedAreaId, RestrictedTradingAreaSaveRequest request)
    {
        var user = GetAuthorizedUser();
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Stall management access is required." });
        }

        if (restrictedAreaId <= 0)
        {
            return BadRequest(new { message = "Select a valid restricted area." });
        }

        if (string.IsNullOrWhiteSpace(request.StreetName) || string.IsNullOrWhiteSpace(request.AreaCode))
        {
            return BadRequest(new { message = "Street name and area code are required." });
        }

        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_RestrictedTradingAreas_Save");
            AddRestrictedAreaParameters(command, request);
            command.Parameters.AddWithValue("@RestrictedAreaId", restrictedAreaId);
            command.Parameters.AddWithValue("@UserId", user.UserId);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync()
                ? Ok(MapRestrictedTradingArea(reader))
                : NotFound(new { message = "The restricted area could not be found." });
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    [HttpPost("restricted-areas/{restrictedAreaId:int}/archive")]
    public async Task<ActionResult> ArchiveRestrictedArea(int restrictedAreaId)
    {
        var user = GetAuthorizedUser();
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Stall management access is required." });
        }

        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_RestrictedTradingAreas_Archive");
            command.Parameters.AddWithValue("@RestrictedAreaId", restrictedAreaId);
            command.Parameters.AddWithValue("@UserId", user.UserId);

            await command.Connection!.OpenAsync();
            await command.ExecuteNonQueryAsync();
            return Ok(new { message = "Restricted area archived." });
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<StallResponse>> SaveStall(StallSaveRequest request)
    {
        var user = GetAuthorizedUser();
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Stall management access is required." });
        }

        if (string.IsNullOrWhiteSpace(request.StallType) || string.IsNullOrWhiteSpace(request.Address))
        {
            return BadRequest(new { message = "Stall type and address are required." });
        }

        if (request.Latitude == 0 || request.Longitude == 0)
        {
            return BadRequest(new { message = "Pin the stall location on the map." });
        }

        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Stalls_Create");
            command.Parameters.AddWithValue("@StallName", string.IsNullOrWhiteSpace(request.StallName) ? DBNull.Value : request.StallName.Trim());
            command.Parameters.AddWithValue("@StallType", request.StallType.Trim());
            command.Parameters.AddWithValue("@Address", request.Address.Trim());
            command.Parameters.AddWithValue("@AreaName", string.IsNullOrWhiteSpace(request.AreaName) ? DBNull.Value : request.AreaName.Trim());
            command.Parameters.AddWithValue("@Latitude", request.Latitude);
            command.Parameters.AddWithValue("@Longitude", request.Longitude);
            command.Parameters.AddWithValue("@CreatedByUserId", user.UserId);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync()
                ? Ok(MapStall(reader))
                : StatusCode(StatusCodes.Status500InternalServerError, new { message = "The stall could not be saved." });
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    [HttpPut("{stallId:int}")]
    public async Task<ActionResult<StallResponse>> UpdateStall(int stallId, StallSaveRequest request)
    {
        var user = GetAuthorizedUser();
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Stall management access is required." });
        }

        if (stallId <= 0)
        {
            return BadRequest(new { message = "Select a valid stall." });
        }

        if (string.IsNullOrWhiteSpace(request.StallType) || string.IsNullOrWhiteSpace(request.Address))
        {
            return BadRequest(new { message = "Stall type and address are required." });
        }

        if (request.Latitude == 0 || request.Longitude == 0)
        {
            return BadRequest(new { message = "Pin the stall location on the map." });
        }

        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Stalls_Update");
            command.Parameters.AddWithValue("@StallId", stallId);
            command.Parameters.AddWithValue("@StallName", string.IsNullOrWhiteSpace(request.StallName) ? DBNull.Value : request.StallName.Trim());
            command.Parameters.AddWithValue("@StallType", request.StallType.Trim());
            command.Parameters.AddWithValue("@Address", request.Address.Trim());
            command.Parameters.AddWithValue("@AreaName", string.IsNullOrWhiteSpace(request.AreaName) ? DBNull.Value : request.AreaName.Trim());
            command.Parameters.AddWithValue("@Latitude", request.Latitude);
            command.Parameters.AddWithValue("@Longitude", request.Longitude);
            command.Parameters.AddWithValue("@UpdatedByUserId", user.UserId);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync()
                ? Ok(MapStall(reader))
                : NotFound(new { message = "The stall could not be found." });
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    [HttpPost("{stallId:int}/archive")]
    public async Task<ActionResult> ArchiveStall(int stallId)
    {
        var user = GetAuthorizedUser();
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Stall management access is required." });
        }

        if (stallId <= 0)
        {
            return BadRequest(new { message = "Select a valid stall." });
        }

        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Stalls_Archive");
            command.Parameters.AddWithValue("@StallId", stallId);
            command.Parameters.AddWithValue("@ArchivedByUserId", user.UserId);

            await command.Connection!.OpenAsync();
            await command.ExecuteNonQueryAsync();
            return Ok(new { message = "Stall archived." });
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    [HttpPost("allocate")]
    public async Task<ActionResult<StallAllocationResponse>> AllocateStall(StallAllocationRequest request)
    {
        var user = GetAuthorizedUser();
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Stall management access is required." });
        }

        if (request.ApplicationId <= 0 || request.StallId <= 0)
        {
            return BadRequest(new { message = "Select an application and a stall." });
        }

        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Stalls_AllocateToApplication");
            command.Parameters.AddWithValue("@ApplicationId", request.ApplicationId);
            command.Parameters.AddWithValue("@StallId", request.StallId);
            command.Parameters.AddWithValue("@AllocatedByUserId", user.UserId);
            command.Parameters.AddWithValue("@AllocatedByDisplayName", string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "The stall allocation could not be saved." });
            }

            return Ok(new StallAllocationResponse
            {
                ApplicationStallAllocationId = reader.GetInt32(reader.GetOrdinal("ApplicationStallAllocationId")),
                ApplicationId = reader.GetInt32(reader.GetOrdinal("ApplicationId")),
                StallId = reader.GetInt32(reader.GetOrdinal("StallId")),
                StallNumber = reader.GetString(reader.GetOrdinal("StallNumber")),
                StallName = reader.GetString(reader.GetOrdinal("StallName")),
                IsRestrictedArea = reader.GetBoolean(reader.GetOrdinal("IsRestrictedArea")),
                InspectionGroupName = reader.GetString(reader.GetOrdinal("InspectionGroupName"))
            });
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    private UserTokenPayload? GetAuthorizedUser()
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        return user is not null && StallManagementGroups.Any(group => user.Groups.Contains(group, StringComparer.OrdinalIgnoreCase))
            ? user
            : null;
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
            AreaName = reader.IsDBNull(reader.GetOrdinal("AreaName")) ? null : reader.GetString(reader.GetOrdinal("AreaName")),
            Latitude = reader.GetDecimal(reader.GetOrdinal("Latitude")),
            Longitude = reader.GetDecimal(reader.GetOrdinal("Longitude")),
            IsRestrictedArea = reader.GetBoolean(reader.GetOrdinal("IsRestrictedArea")),
            RestrictedStreetName = reader.IsDBNull(reader.GetOrdinal("RestrictedStreetName")) ? null : reader.GetString(reader.GetOrdinal("RestrictedStreetName")),
            RestrictedAreaCode = reader.IsDBNull(reader.GetOrdinal("RestrictedAreaCode")) ? null : reader.GetString(reader.GetOrdinal("RestrictedAreaCode")),
            IsOccupied = reader.GetBoolean(reader.GetOrdinal("IsOccupied")),
            AllocatedTrackingNumber = GetOptionalString(reader, "AllocatedTrackingNumber"),
            AllocatedBusinessName = GetOptionalString(reader, "AllocatedBusinessName"),
            AllocatedApplicationStatus = GetOptionalString(reader, "AllocatedApplicationStatus"),
            AllocatedApplicationStage = GetOptionalString(reader, "AllocatedApplicationStage")
        };
    }

    private static string? GetOptionalString(IDataRecord reader, string columnName)
    {
        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (!string.Equals(reader.GetName(index), columnName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return reader.IsDBNull(index) ? null : reader.GetString(index);
        }

        return null;
    }

    private static void AddRestrictedAreaParameters(SqlCommand command, RestrictedTradingAreaSaveRequest request)
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

    private static RestrictedTradingAreaResponse MapRestrictedTradingArea(IDataRecord reader)
    {
        return new RestrictedTradingAreaResponse
        {
            RestrictedAreaId = reader.GetInt32(reader.GetOrdinal("RestrictedAreaId")),
            StreetName = reader.GetString(reader.GetOrdinal("StreetName")),
            AreaCode = reader.GetString(reader.GetOrdinal("AreaCode")),
            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
            DisplayName = reader.IsDBNull(reader.GetOrdinal("DisplayName")) ? null : reader.GetString(reader.GetOrdinal("DisplayName")),
            CenterLatitude = reader.IsDBNull(reader.GetOrdinal("CenterLatitude")) ? null : reader.GetDecimal(reader.GetOrdinal("CenterLatitude")),
            CenterLongitude = reader.IsDBNull(reader.GetOrdinal("CenterLongitude")) ? null : reader.GetDecimal(reader.GetOrdinal("CenterLongitude")),
            MapZoom = reader.IsDBNull(reader.GetOrdinal("MapZoom")) ? null : reader.GetInt32(reader.GetOrdinal("MapZoom")),
            GeometryType = reader.IsDBNull(reader.GetOrdinal("GeometryType")) ? null : reader.GetString(reader.GetOrdinal("GeometryType")),
            GeometryJson = reader.IsDBNull(reader.GetOrdinal("GeometryJson")) ? null : reader.GetString(reader.GetOrdinal("GeometryJson")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
        };
    }
}
