using BCLMSApi.Data;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StallsController(StallsRepository repository,IUserTokenService tokens):ControllerBase
{
    private static readonly string[] Groups=["Super User","Admin Officer","Director","Functional Head"];
    [HttpGet] public async Task<ActionResult<List<StallResponse>>> GetStalls(){return GetUser() is null?Forbidden("Stall management access is required."):Ok(await repository.GetAllAsync());}
    [HttpGet("applications")] public async Task<ActionResult<List<StallApplicationOptionResponse>>> GetApplicationsForAllocation(){return GetUser() is null?Forbidden("Stall management access is required."):Ok(await repository.GetApplicationOptionsAsync());}
    [HttpGet("restricted-areas")] public async Task<ActionResult<List<RestrictedTradingAreaResponse>>> GetRestrictedAreas(){if(tokens.GetValidTokenPayload(Request.Headers.Authorization) is null)return Forbidden("Sign in to view restricted trading areas.");return Ok(await repository.GetAreasAsync());}
    [HttpPost("restricted-areas")] public Task<ActionResult<RestrictedTradingAreaResponse>> SaveRestrictedArea(RestrictedTradingAreaSaveRequest q)=>SaveArea(null,q);
    [HttpPut("restricted-areas/{id:int}")] public Task<ActionResult<RestrictedTradingAreaResponse>> UpdateRestrictedArea(int id,RestrictedTradingAreaSaveRequest q)=>SaveArea(id,q);
    private async Task<ActionResult<RestrictedTradingAreaResponse>> SaveArea(int? id,RestrictedTradingAreaSaveRequest q){var u=GetUser();if(u is null)return Forbidden("Stall management access is required.");if(id is <=0)return BadRequest(new{message="Select a valid restricted area."});if(string.IsNullOrWhiteSpace(q.StreetName)||string.IsNullOrWhiteSpace(q.AreaCode))return BadRequest(new{message="Street name and area code are required."});var x=await repository.SaveAreaAsync(id,q,u.UserId);return x is null?NotFound(new{message="The restricted area could not be found."}):Ok(x);}
    [HttpPost("restricted-areas/{id:int}/archive")] public async Task<ActionResult> ArchiveRestrictedArea(int id){var u=GetUser();if(u is null)return Forbidden("Stall management access is required.");await repository.ArchiveAreaAsync(id,u.UserId);return Ok(new{message="Restricted area archived."});}
    [HttpPost] public Task<ActionResult<StallResponse>> SaveStall(StallSaveRequest q)=>SaveStallCore(null,q);
    [HttpPut("{id:int}")] public Task<ActionResult<StallResponse>> UpdateStall(int id,StallSaveRequest q)=>SaveStallCore(id,q);
    private async Task<ActionResult<StallResponse>> SaveStallCore(int? id,StallSaveRequest q){var u=GetUser();if(u is null)return Forbidden("Stall management access is required.");if(id is <=0)return BadRequest(new{message="Select a valid stall."});if(string.IsNullOrWhiteSpace(q.StallType)||string.IsNullOrWhiteSpace(q.Address))return BadRequest(new{message="Stall type and address are required."});if(q.Latitude==0||q.Longitude==0)return BadRequest(new{message="Pin the stall location on the map."});var x=await repository.SaveAsync(id,q,u.UserId);return x is null?NotFound(new{message="The stall could not be found."}):Ok(x);}
    [HttpPost("{id:int}/archive")] public async Task<ActionResult> ArchiveStall(int id){var u=GetUser();if(u is null)return Forbidden("Stall management access is required.");if(id<=0)return BadRequest(new{message="Select a valid stall."});await repository.ArchiveAsync(id,u.UserId);return Ok(new{message="Stall archived."});}
    [HttpPost("allocate")] public async Task<ActionResult<StallAllocationResponse>> AllocateStall(StallAllocationRequest q){var u=GetUser();if(u is null)return Forbidden("Stall management access is required.");if(q.ApplicationId<=0||q.StallId<=0)return BadRequest(new{message="Select an application and a stall."});var x=await repository.AllocateAsync(q,u.UserId,string.IsNullOrWhiteSpace(u.DisplayName)?u.Username:u.DisplayName);return x is null?StatusCode(500,new{message="The stall allocation could not be saved."}):Ok(x);}
    private UserTokenPayload? GetUser(){var u=tokens.GetValidTokenPayload(Request.Headers.Authorization);return u is not null&&Groups.Any(g=>u.Groups.Contains(g,StringComparer.OrdinalIgnoreCase))?u:null;}
    private ActionResult Forbidden(string message)=>StatusCode(403,new{message});
}
