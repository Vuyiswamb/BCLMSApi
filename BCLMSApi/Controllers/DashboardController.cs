using BCLMSApi.Data;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(DashboardRepository repository,IUserTokenService tokens,IFormalBusinessService businessService):ControllerBase
{
    [HttpGet("customers")]
    public async Task<IActionResult> Customers(CancellationToken cancellationToken)
    {
        var user=tokens.GetValidTokenPayload(Request.Headers.Authorization);
        string[] allowed=["Super User","Admin Officer","Licensing Officer","Compliance Officer","Metro Police","City Planning","Health Department","Fire Department","Senior Specialist","Functional Head","Director"];
        if(user is null||user.IsCustomer||!user.Groups.Any(g=>allowed.Contains(g,StringComparer.OrdinalIgnoreCase)))return StatusCode(403,new{message="An official dashboard account is required."});
        var townships=(await businessService.GetTownshipsAsync()).ToDictionary(x=>x.TownshipId);var all=user.IsSuperUser||user.HasAllRegions;var regions=user.Regions.Select(x=>x.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);var customers=new Dictionary<int,DashboardCustomer>();
        foreach(var row in await repository.GetCustomersAsync(cancellationToken)){var region=row.RegionName?.Trim();if(row.TownshipId is int townshipId&&townships.TryGetValue(townshipId,out var township)&&!string.IsNullOrWhiteSpace(township.RegionName))region=township.RegionName.Trim();region=string.IsNullOrWhiteSpace(region)?"Not captured":region;if(!all&&!regions.Contains(region))continue;if(!customers.TryGetValue(row.UserId,out var customer))customers[row.UserId]=customer=new(){CustomerId=row.UserId};if(!customer.Regions.Contains(region,StringComparer.OrdinalIgnoreCase))customer.Regions.Add(region);if(row.Latitude is null||row.Longitude is null||!double.IsFinite(row.Latitude.Value)||!double.IsFinite(row.Longitude.Value)||row.Latitude<-85.05112878||row.Latitude>85.05112878||row.Longitude<-180||row.Longitude>180||(row.Latitude==0&&row.Longitude==0))continue;var location=new DashboardCustomerLocation(row.BusinessName??"Business",region,row.Latitude.Value,row.Longitude.Value);if(!customer.Locations.Contains(location))customer.Locations.Add(location);}
        return Ok(customers.Values);
    }

    [HttpGet("customer-registrations")]
    public async Task<IActionResult> CustomerRegistrations(CancellationToken cancellationToken)
    {
        var user = tokens.GetValidTokenPayload(Request.Headers.Authorization);
        string[] allowed = ["Super User", "Admin Officer", "Licensing Officer", "Compliance Officer", "Metro Police", "City Planning", "Health Department", "Fire Department", "Senior Specialist", "Functional Head", "Director"];
        if (user is null || user.IsCustomer || !user.Groups.Any(g => allowed.Contains(g, StringComparer.OrdinalIgnoreCase)))
        {
            return StatusCode(403, new { message = "An official dashboard account is required." });
        }

        return Ok(await repository.GetCustomerRegistrationCountsAsync(cancellationToken));
    }
}
public class DashboardCustomer{public int CustomerId{get;set;}public List<string> Regions{get;set;}=[];public List<DashboardCustomerLocation> Locations{get;set;}=[];}
public record DashboardCustomerLocation(string BusinessName,string Region,double Latitude,double Longitude);
