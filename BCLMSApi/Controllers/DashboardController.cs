using BCLMSApi.Data;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(Datalayer data, IUserTokenService tokens, IFormalBusinessService businessService) : ControllerBase
{
    [HttpGet("customers")]
    public async Task<IActionResult> Customers(CancellationToken cancellationToken)
    {
        var user = tokens.GetValidTokenPayload(Request.Headers.Authorization);
        string[] allowedGroups = ["Super User", "Admin Officer", "Licensing Officer", "Compliance Officer", "Metro Police", "City Planning", "Health Department", "Fire Department", "Senior Specialist", "Functional Head", "Director"];
        if (user is null || user.IsCustomer || !user.Groups.Any(g => allowedGroups.Contains(g, StringComparer.OrdinalIgnoreCase)))
            return StatusCode(403, new { message = "An official dashboard account is required." });

        var townships = (await businessService.GetTownshipsAsync()).ToDictionary(t => t.TownshipId);
        var allRegions = user.IsSuperUser || user.HasAllRegions;
        var allowedRegions = user.Regions.Select(r => r.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Include registrations without applications; never infer the number of users from application rows.
        await using var command = data.CreateTextCommand("""
            WITH CustomerLocations AS (
                SELECT b.UserId, b.BusinessName, b.TownshipId, a.RegionName, a.Latitude, a.Longitude
                FROM dbo.CustomerBusinesses b
                OUTER APPLY (
                    SELECT TOP (1) x.RegionName, x.Latitude, x.Longitude
                    FROM dbo.Applications x
                    WHERE x.BusinessId = b.BusinessId AND x.UserId = b.UserId
                    ORDER BY x.SubmittedDate DESC, x.ApplicationId DESC
                ) a
                WHERE b.IsActive = 1
                UNION ALL
                SELECT a.UserId, a.BusinessName, a.TownshipId, a.RegionName, a.Latitude, a.Longitude
                FROM dbo.Applications a
                WHERE a.BusinessId IS NULL
            )
            SELECT u.UserId, l.BusinessName, l.TownshipId, l.RegionName, l.Latitude, l.Longitude
            FROM dbo.Users u
            LEFT JOIN CustomerLocations l ON l.UserId = u.UserId
            WHERE EXISTS (
                SELECT 1 FROM dbo.UserGroups ug
                INNER JOIN dbo.Groups g ON g.GroupId = ug.GroupId
                WHERE ug.UserId = u.UserId AND g.GroupName = 'Customer'
            )
            ORDER BY u.UserId;
            """);
        await command.Connection!.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var customers = new Dictionary<int, DashboardCustomer>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var region = reader.IsDBNull(3) ? null : reader.GetString(3).Trim();
            if (!reader.IsDBNull(2) && townships.TryGetValue(reader.GetInt32(2), out var township)
                && !string.IsNullOrWhiteSpace(township.RegionName))
                region = township.RegionName.Trim();
            region = string.IsNullOrWhiteSpace(region) ? "Not captured" : region;
            if (!allRegions && !allowedRegions.Contains(region)) continue;
            var id = reader.GetInt32(0);
            if (!customers.TryGetValue(id, out var customer))
                customers[id] = customer = new DashboardCustomer { CustomerId = id };
            if (!customer.Regions.Contains(region, StringComparer.OrdinalIgnoreCase)) customer.Regions.Add(region);
            if (reader.IsDBNull(4) || reader.IsDBNull(5)) continue;
            var latitude = Convert.ToDouble(reader.GetValue(4));
            var longitude = Convert.ToDouble(reader.GetValue(5));
            if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude < -85.05112878 || latitude > 85.05112878
                || longitude < -180 || longitude > 180 || (latitude == 0 && longitude == 0)) continue;
            var location = new DashboardCustomerLocation(reader.IsDBNull(1) ? "Business" : reader.GetString(1), region, latitude, longitude);
            if (!customer.Locations.Contains(location)) customer.Locations.Add(location);
        }
        return Ok(customers.Values);
    }
}

public class DashboardCustomer
{
    public int CustomerId { get; set; }
    public List<string> Regions { get; set; } = [];
    public List<DashboardCustomerLocation> Locations { get; set; } = [];
}

public record DashboardCustomerLocation(string BusinessName, string Region, double Latitude, double Longitude);
