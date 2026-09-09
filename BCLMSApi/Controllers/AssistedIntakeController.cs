using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using System.Transactions;
using BCLMSApi.Data;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/AssistedIntake")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AssistedIntakeController(Datalayer data, IAuthService auth, IFormalBusinessService applications,
    IUserTokenService tokens) : ControllerBase
{
    public static bool CanAssist(UserTokenPayload? user) => user is not null && !user.IsCustomer &&
        user.Groups.Any(group => new[] { "Super User", "Admin Officer", "Office Support" }.Contains(group, StringComparer.OrdinalIgnoreCase));

    private UserTokenPayload Staff() {
        var user = tokens.GetValidTokenPayload(Request.Headers.Authorization);
        if (!CanAssist(user)) throw new UnauthorizedAccessException("Office Support, Admin Officer or Super User access is required.");
        return user!;
    }

    private static TransactionScope Transaction() => new(TransactionScopeOption.Required,
        new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.Serializable, Timeout = TimeSpan.FromMinutes(2) },
        TransactionScopeAsyncFlowOption.Enabled);

    private async Task<IActionResult> Execute(Func<Task<object>> action) {
        try { return Ok(await action()); }
        catch (UnauthorizedAccessException error) { return StatusCode(403, new { message = error.Message }); }
        catch (KeyNotFoundException error) { return NotFound(new { message = error.Message }); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        catch (InvalidOperationException) { return Conflict(new { message = "The request could not be completed. Check the account details and try again." }); }
        catch (SqlException error) when (error.Number is 2601 or 2627) { return Conflict(new { message = "An account already exists. Select Existing customer to continue without creating another account." }); }
    }

    [HttpGet]
    public Task<IActionResult> List() => Execute(async () => {
        var staff = Staff();
        var results = new List<object>();
        await using var command = data.CreateTextCommand("""
            SELECT TOP (50) intake.IntakeId, customer.DisplayName, customer.EmailAddress, intake.ApplicationId, application.TrackingNumber
            FROM dbo.AssistedIntakes intake
            JOIN dbo.Users customer ON customer.UserId = intake.CustomerUserId
            LEFT JOIN dbo.Applications application ON application.ApplicationId = intake.ApplicationId
            WHERE intake.OperatorUserId = @StaffId
            ORDER BY intake.IntakeId DESC;
            """);
        command.Parameters.AddWithValue("@StaffId", staff.UserId);
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) results.Add(new { intakeId = reader.GetInt32(0), fullName = reader.GetString(1), email = reader.GetString(2),
            applicationId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3), trackingNumber = reader.IsDBNull(4) ? null : reader.GetString(4) });
        return results;
    });

    [HttpPost]
    public Task<IActionResult> Start(AssistedRegistrationRequest request) => Execute(async () => {
        var staff = Staff();
        if (!request.OwnerPresent) throw new ArgumentException("Confirm that the business owner is present and has requested assistance.");
        if (string.IsNullOrWhiteSpace(request.Customer.FullName) || request.Customer.FullName.Length > 150 ||
            string.IsNullOrWhiteSpace(request.Customer.IdNumber) || request.Customer.IdNumber.Length > 80 ||
            string.IsNullOrWhiteSpace(request.Customer.Phone) || request.Customer.Phone.Length > 40 ||
            request.Customer.Email.Length > 80 || !new EmailAddressAttribute().IsValid(request.Customer.Email))
            throw new ArgumentException("Enter the owner's name, ID or passport number, valid email address and phone number.");
        using var transaction = Transaction();
        int customerId;
        if (request.ExistingCustomer) {
            await using var lookup = data.CreateTextCommand("""
                SELECT users.UserId FROM dbo.Users users
                WHERE users.EmailAddress = @Email AND users.IsActive = 1
                  AND EXISTS (SELECT 1 FROM dbo.UserGroups ug JOIN dbo.Groups g ON g.GroupId = ug.GroupId WHERE ug.UserId = users.UserId AND g.GroupName = 'Customer')
                  AND NOT EXISTS (SELECT 1 FROM dbo.UserGroups ug JOIN dbo.Groups g ON g.GroupId = ug.GroupId WHERE ug.UserId = users.UserId AND g.GroupName <> 'Customer');
                """);
            lookup.Parameters.AddWithValue("@Email", request.Customer.Email.Trim());
            await lookup.Connection!.OpenAsync();
            var id = await lookup.ExecuteScalarAsync();
            if (id is null) throw new KeyNotFoundException("No active Customer account was found for this email address.");
            customerId = Convert.ToInt32(id);
        } else {
            // Create a normal customer account; never sign the operator in as the customer.
            customerId = (await auth.RegisterCustomerAsync(request.Customer, sendWelcomeEmail: false)).UserId;
        }
        int intakeId;
        await using (var command = data.CreateTextCommand("""
            INSERT dbo.AssistedIntakes (OperatorUserId, CustomerUserId, IdNumber, Phone)
            OUTPUT INSERTED.IntakeId VALUES (@StaffId, @CustomerId, @IdNumber, @Phone);
            """)) {
            command.Parameters.AddWithValue("@StaffId", staff.UserId);
            command.Parameters.AddWithValue("@CustomerId", customerId);
            command.Parameters.AddWithValue("@IdNumber", request.Customer.IdNumber.Trim());
            command.Parameters.AddWithValue("@Phone", request.Customer.Phone.Trim());
            await command.Connection!.OpenAsync();
            intakeId = Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        transaction.Complete();
        return new { intakeId, message = "Customer account ready. The owner can sign in using their email address and password." };
    });

    [HttpGet("{intakeId:int}")]
    public Task<IActionResult> Get(int intakeId) => Execute(async () => {
        var staff = Staff();
        var intake = await Read(intakeId, staff);
        var businesses = await applications.GetCustomerBusinessesAsync(Customer(intake));
        var townships = await applications.GetTownshipsAsync();
        return new { intake.IntakeId, intake.FullName, intake.Email, intake.IdNumber, intake.Phone, intake.BusinessId, intake.ApplicationId,
            trackingNumber = intake.Response is null ? null : JsonSerializer.Deserialize<ApplicationSubmitResponse>(intake.Response)?.TrackingNumber,
            businesses = businesses.Where(business => townships.Any(township => township.TownshipId == business.TownshipId && InRegion(staff, township.RegionName))) };
    });

    [HttpPost("{intakeId:int}/business")]
    public Task<IActionResult> SaveBusiness(int intakeId, AssistedBusinessRequest request) => Execute(async () => {
        var staff = Staff();
        using var transaction = Transaction();
        var intake = await Read(intakeId, staff);
        if (intake.ApplicationId.HasValue) throw new ArgumentException("This intake already has an application.");
        CustomerBusinessResponse business;
        if (request.BusinessId.HasValue) {
            business = await applications.GetCustomerBusinessAsync(Customer(intake), request.BusinessId.Value)
                ?? throw new KeyNotFoundException("Business not found for this customer.");
            await CheckRegion(staff, business.TownshipId);
        } else {
            await CheckRegion(staff, request.Business.TownshipId);
            // A retry reuses the business already created for this intake.
            business = intake.BusinessId.HasValue
                ? await applications.GetCustomerBusinessAsync(Customer(intake), intake.BusinessId.Value) ?? throw new KeyNotFoundException()
                : await applications.CreateCustomerBusinessAsync(Customer(intake), request.Business);
        }
        await using var command = data.CreateTextCommand("UPDATE dbo.AssistedIntakes SET BusinessId = @BusinessId, UpdatedAtUtc = SYSUTCDATETIME() WHERE IntakeId = @IntakeId;");
        command.Parameters.AddWithValue("@BusinessId", business.BusinessId);
        command.Parameters.AddWithValue("@IntakeId", intakeId);
        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
        transaction.Complete();
        return business;
    });

    [HttpPost("{intakeId:int}/workshop")]
    public Task<IActionResult> Workshop(int intakeId, AssistedWorkshopRequest request) => Execute(async () => {
        var staff = Staff();
        if (!request.AttendanceConfirmed) throw new ArgumentException("Confirm that the owner has attended the workshop.");
        using var transaction = Transaction();
        var intake = await Read(intakeId, staff);
        if (intake.ApplicationId.HasValue) throw new ArgumentException("This intake already has an application.");
        var business = await LinkedBusiness(intake, staff);
        await using var command = data.CreateTextCommand("""
            UPDATE dbo.CustomerBusinesses SET WorkshopAttended = 1,
                WorkshopAttendedDate = COALESCE(WorkshopAttendedDate, SYSUTCDATETIME()), ModifiedDate = SYSUTCDATETIME()
            WHERE BusinessId = @BusinessId AND UserId = @CustomerId;
            UPDATE dbo.WorkshopAttendanceRequests SET Status = 'Attended',
                AttendedDate = COALESCE(AttendedDate, SYSUTCDATETIME()), ModifiedDate = SYSUTCDATETIME()
            WHERE BusinessId = @BusinessId AND UserId = @CustomerId AND IsActive = 1;
            UPDATE dbo.AssistedIntakes SET WorkshopRecordedAtUtc = COALESCE(WorkshopRecordedAtUtc, SYSUTCDATETIME()), UpdatedAtUtc = SYSUTCDATETIME()
            WHERE IntakeId = @IntakeId;
            """);
        command.Parameters.AddWithValue("@BusinessId", business.BusinessId);
        command.Parameters.AddWithValue("@CustomerId", intake.CustomerUserId);
        command.Parameters.AddWithValue("@IntakeId", intakeId);
        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
        transaction.Complete();
        return new { message = "Workshop attendance recorded." };
    });

    [HttpGet("{intakeId:int}/has-application")]
    public Task<IActionResult> HasApplication(int intakeId) => Execute(async () => {
        var staff = Staff();
        var intake = await Read(intakeId, staff);
        var business = await LinkedBusiness(intake, staff);
        return new { hasApplication = await applications.CustomerBusinessHasApplicationAsync(Customer(intake), business.BusinessId) };
    });

    [HttpPost("{intakeId:int}/application")]
    public Task<IActionResult> Submit(int intakeId, ApplicationSubmitRequest request) => Execute(async () => {
        var staff = Staff();
        using var transaction = Transaction();
        var intake = await Read(intakeId, staff);
        var business = await LinkedBusiness(intake, staff);
        if (intake.Response is not null) { transaction.Complete(); return JsonSerializer.Deserialize<ApplicationSubmitResponse>(intake.Response)!; }
        if (request.BusinessId != business.BusinessId) throw new ArgumentException("Select the business linked to this assisted intake.");
        var separateTradingLocation = request.LicenceType.Contains("Event", StringComparison.OrdinalIgnoreCase)
            || request.LicenceType.Contains("Food Vending", StringComparison.OrdinalIgnoreCase);
        var township = await CheckRegion(staff, separateTradingLocation ? request.TownshipId : business.TownshipId);
        request.TownshipId = township.TownshipId;
        request.RegionName = township.RegionName;
        request.AreaCategory = township.AreaCategory;
        request.WardNumber = township.WardNumber;
        request.ApplicantName = intake.FullName;
        request.Email = intake.Email;
        request.IdNumber = intake.IdNumber;
        request.Phone = intake.Phone;
        var result = await applications.SubmitApplicationAsync(request, intake.CustomerUserId);
        await using var command = data.CreateTextCommand("""
            UPDATE dbo.AssistedIntakes SET ApplicationId = @ApplicationId, ApplicationResponse = @Response, UpdatedAtUtc = SYSUTCDATETIME()
            WHERE IntakeId = @IntakeId;
            """);
        command.Parameters.AddWithValue("@ApplicationId", result.ApplicationId);
        command.Parameters.AddWithValue("@Response", JsonSerializer.Serialize(result));
        command.Parameters.AddWithValue("@IntakeId", intakeId);
        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
        transaction.Complete();
        return result;
    });

    [HttpPost("{intakeId:int}/attachments")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public Task<IActionResult> Upload(int intakeId, [FromForm] AttachmentUploadRequest request) => Execute(async () => {
        var staff = Staff();
        var intake = await Read(intakeId, staff);
        await LinkedBusiness(intake, staff);
        if (!intake.ApplicationId.HasValue) throw new ArgumentException("Save the application before uploading documents.");
        var application = await applications.GetInternalApplicationDetailAsync(intake.ApplicationId.Value, Customer(intake))
            ?? throw new KeyNotFoundException("Application not found.");
        if (!InRegion(staff, application.RegionName)) throw new UnauthorizedAccessException("This application is outside your assigned regions.");
        return await applications.UploadAttachmentAsync(intake.ApplicationId.Value, request, Customer(intake));
    });

    private static bool InRegion(UserTokenPayload staff, string? region) => staff.IsSuperUser || staff.HasAllRegions ||
        (region is not null && staff.Regions.Contains(region, StringComparer.OrdinalIgnoreCase));

    private async Task<TownshipResponse> CheckRegion(UserTokenPayload staff, int? townshipId) {
        var township = (await applications.GetTownshipsAsync()).FirstOrDefault(item => item.TownshipId == townshipId)
            ?? throw new ArgumentException("Select a valid area / suburb.");
        if (!InRegion(staff, township.RegionName)) throw new UnauthorizedAccessException("This area is outside your assigned regions.");
        return township;
    }

    private static UserTokenPayload Customer(Intake intake) => new() { UserId = intake.CustomerUserId, DisplayName = intake.FullName,
        Username = intake.Email, Groups = ["Customer"] };

    private async Task<CustomerBusinessResponse> LinkedBusiness(Intake intake, UserTokenPayload staff) {
        if (!intake.BusinessId.HasValue) throw new ArgumentException("Select or register a business first.");
        var business = await applications.GetCustomerBusinessAsync(Customer(intake), intake.BusinessId.Value)
            ?? throw new KeyNotFoundException("Business not found for this customer.");
        await CheckRegion(staff, business.TownshipId);
        return business;
    }

    private async Task<Intake> Read(int intakeId, UserTokenPayload staff) {
        await using var command = data.CreateTextCommand("""
            SELECT intake.IntakeId, intake.CustomerUserId, customer.DisplayName, customer.EmailAddress,
                intake.IdNumber, intake.Phone, intake.BusinessId, intake.ApplicationId, intake.ApplicationResponse
            FROM dbo.AssistedIntakes intake WITH (UPDLOCK)
            JOIN dbo.Users customer ON customer.UserId = intake.CustomerUserId
            WHERE intake.IntakeId = @IntakeId AND intake.OperatorUserId = @StaffId AND customer.IsActive = 1
              AND EXISTS (SELECT 1 FROM dbo.UserGroups ug JOIN dbo.Groups g ON g.GroupId = ug.GroupId WHERE ug.UserId = customer.UserId AND g.GroupName = 'Customer')
              AND NOT EXISTS (SELECT 1 FROM dbo.UserGroups ug JOIN dbo.Groups g ON g.GroupId = ug.GroupId WHERE ug.UserId = customer.UserId AND g.GroupName <> 'Customer');
            """);
        command.Parameters.AddWithValue("@IntakeId", intakeId);
        command.Parameters.AddWithValue("@StaffId", staff.UserId);
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new KeyNotFoundException("Assisted intake not found for your account.");
        return new Intake(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetInt32(6), reader.IsDBNull(7) ? null : reader.GetInt32(7), reader.IsDBNull(8) ? null : reader.GetString(8));
    }

    private record Intake(int IntakeId, int CustomerUserId, string FullName, string Email, string IdNumber, string Phone, int? BusinessId, int? ApplicationId, string? Response);
}

public class AssistedRegistrationRequest {
    [Required] public RegisterCustomerRequest Customer { get; set; } = new();
    public bool ExistingCustomer { get; set; }
    public bool OwnerPresent { get; set; }
}
public class AssistedBusinessRequest {
    public int? BusinessId { get; set; }
    [Required] public CustomerBusinessSaveRequest Business { get; set; } = new();
}
public class AssistedWorkshopRequest { public bool AttendanceConfirmed { get; set; } }
