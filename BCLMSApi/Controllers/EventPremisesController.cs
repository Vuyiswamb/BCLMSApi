using System.Data;
using BCLMSApi.Data;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventPremisesController(Datalayer datalayer, IUserTokenService userTokenService) : ControllerBase
{
    private static readonly string[] EventPremiseManagementGroups = ["Super User", "Admin Officer", "Director", "Functional Head"];

    [HttpGet]
    public async Task<ActionResult<List<EventPremiseResponse>>> GetEventPremises()
    {
        var user = GetAuthorizedUser();
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Event premise management access is required." });
        }

        try
        {
            await EnsureEventPremisesTableAsync();

            var premises = new List<EventPremiseResponse>();
            await using var command = datalayer.CreateTextCommand("""
                SELECT EventPremiseId, Name, StreetNameAndNumber, ContactDetails, Latitude, Longitude, IsActive
                FROM dbo.EventPremises
                ORDER BY IsActive DESC, Name ASC;
                """);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                premises.Add(MapEventPremise(reader));
            }

            return Ok(premises);
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<EventPremiseResponse>> CreateEventPremise(EventPremiseSaveRequest request)
    {
        var user = GetAuthorizedUser();
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Event premise management access is required." });
        }

        var validationMessage = ValidateRequest(request);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return BadRequest(new { message = validationMessage });
        }

        try
        {
            await EnsureEventPremisesTableAsync();

            await using var command = datalayer.CreateTextCommand("""
                INSERT INTO dbo.EventPremises
                    (Name, StreetNameAndNumber, ContactDetails, Latitude, Longitude, IsActive, CreatedByUserId, CreatedDate)
                OUTPUT inserted.EventPremiseId, inserted.Name, inserted.StreetNameAndNumber, inserted.ContactDetails, inserted.Latitude, inserted.Longitude, inserted.IsActive
                VALUES
                    (@Name, @StreetNameAndNumber, @ContactDetails, @Latitude, @Longitude, 1, @UserId, SYSUTCDATETIME());
                """);
            AddParameters(command, request, user.UserId);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync()
                ? Ok(MapEventPremise(reader))
                : StatusCode(StatusCodes.Status500InternalServerError, new { message = "The event premise could not be saved." });
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    [HttpPut("{eventPremiseId:int}")]
    public async Task<ActionResult<EventPremiseResponse>> UpdateEventPremise(int eventPremiseId, EventPremiseSaveRequest request)
    {
        var user = GetAuthorizedUser();
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Event premise management access is required." });
        }

        if (eventPremiseId <= 0)
        {
            return BadRequest(new { message = "Select a valid event premise." });
        }

        var validationMessage = ValidateRequest(request);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return BadRequest(new { message = validationMessage });
        }

        try
        {
            await EnsureEventPremisesTableAsync();

            await using var command = datalayer.CreateTextCommand("""
                UPDATE dbo.EventPremises
                SET Name = @Name,
                    StreetNameAndNumber = @StreetNameAndNumber,
                    ContactDetails = @ContactDetails,
                    Latitude = @Latitude,
                    Longitude = @Longitude,
                    UpdatedByUserId = @UserId,
                    UpdatedDate = SYSUTCDATETIME()
                OUTPUT inserted.EventPremiseId, inserted.Name, inserted.StreetNameAndNumber, inserted.ContactDetails, inserted.Latitude, inserted.Longitude, inserted.IsActive
                WHERE EventPremiseId = @EventPremiseId;
                """);
            command.Parameters.AddWithValue("@EventPremiseId", eventPremiseId);
            AddParameters(command, request, user.UserId);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync()
                ? Ok(MapEventPremise(reader))
                : NotFound(new { message = "The event premise could not be found." });
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    [HttpPost("{eventPremiseId:int}/disable")]
    public async Task<ActionResult> DisableEventPremise(int eventPremiseId)
    {
        var user = GetAuthorizedUser();
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Event premise management access is required." });
        }

        if (eventPremiseId <= 0)
        {
            return BadRequest(new { message = "Select a valid event premise." });
        }

        try
        {
            await EnsureEventPremisesTableAsync();

            await using var command = datalayer.CreateTextCommand("""
                UPDATE dbo.EventPremises
                SET IsActive = 0,
                    UpdatedByUserId = @UserId,
                    UpdatedDate = SYSUTCDATETIME()
                WHERE EventPremiseId = @EventPremiseId;

                SELECT @@ROWCOUNT;
                """);
            command.Parameters.AddWithValue("@EventPremiseId", eventPremiseId);
            command.Parameters.AddWithValue("@UserId", user.UserId);

            await command.Connection!.OpenAsync();
            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0
                ? Ok(new { message = "Event premise disabled." })
                : NotFound(new { message = "The event premise could not be found." });
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    private UserTokenPayload? GetAuthorizedUser()
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        return user is not null && EventPremiseManagementGroups.Any(group => user.Groups.Contains(group, StringComparer.OrdinalIgnoreCase))
            ? user
            : null;
    }

    private async Task EnsureEventPremisesTableAsync()
    {
        await using var command = datalayer.CreateTextCommand("""
            IF OBJECT_ID('dbo.EventPremises', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.EventPremises
                (
                    EventPremiseId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EventPremises PRIMARY KEY,
                    Name NVARCHAR(200) NOT NULL,
                    StreetNameAndNumber NVARCHAR(500) NOT NULL,
                    ContactDetails NVARCHAR(500) NOT NULL,
                    Latitude DECIMAL(18, 8) NOT NULL,
                    Longitude DECIMAL(18, 8) NOT NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_EventPremises_IsActive DEFAULT (1),
                    CreatedByUserId INT NULL,
                    CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_EventPremises_CreatedDate DEFAULT (SYSUTCDATETIME()),
                    UpdatedByUserId INT NULL,
                    UpdatedDate DATETIME2(0) NULL
                );
            END;
            """);

        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    private static string ValidateRequest(EventPremiseSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Premise name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.StreetNameAndNumber))
        {
            return "Street name and number are required.";
        }

        if (string.IsNullOrWhiteSpace(request.ContactDetails))
        {
            return "Contact details are required.";
        }

        if (request.Latitude == 0 || request.Longitude == 0)
        {
            return "Pin the event premise location on the map.";
        }

        return string.Empty;
    }

    private static void AddParameters(SqlCommand command, EventPremiseSaveRequest request, int userId)
    {
        command.Parameters.AddWithValue("@Name", request.Name.Trim());
        command.Parameters.AddWithValue("@StreetNameAndNumber", request.StreetNameAndNumber.Trim());
        command.Parameters.AddWithValue("@ContactDetails", request.ContactDetails.Trim());
        command.Parameters.AddWithValue("@Latitude", request.Latitude);
        command.Parameters.AddWithValue("@Longitude", request.Longitude);
        command.Parameters.AddWithValue("@UserId", userId);
    }

    private static EventPremiseResponse MapEventPremise(IDataRecord reader)
    {
        return new EventPremiseResponse
        {
            EventPremiseId = reader.GetInt32(reader.GetOrdinal("EventPremiseId")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            StreetNameAndNumber = reader.GetString(reader.GetOrdinal("StreetNameAndNumber")),
            ContactDetails = reader.GetString(reader.GetOrdinal("ContactDetails")),
            Latitude = reader.GetDecimal(reader.GetOrdinal("Latitude")),
            Longitude = reader.GetDecimal(reader.GetOrdinal("Longitude")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
        };
    }
}
