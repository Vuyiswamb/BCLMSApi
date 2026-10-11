using BCLMSApi.Data;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventPremisesController(EventPremiseRepository repository,IUserTokenService tokens):ControllerBase
{
    private static readonly string[] Groups=["Super User","Admin Officer","Director","Functional Head"];
    [HttpGet] public async Task<ActionResult<List<EventPremiseResponse>>> Get(){if(GetUser() is null)return Forbidden();return Ok(await repository.GetAllAsync());}
    [HttpPost] public async Task<ActionResult<EventPremiseResponse>> Create(EventPremiseSaveRequest request){var user=GetUser();if(user is null)return Forbidden();var error=Validate(request);if(error is not null)return BadRequest(new{message=error});return Ok(await repository.SaveAsync(null,request,user.UserId));}
    [HttpPut("{id:int}")] public async Task<ActionResult<EventPremiseResponse>> Update(int id,EventPremiseSaveRequest request){var user=GetUser();if(user is null)return Forbidden();if(id<=0)return BadRequest(new{message="Select a valid event premise."});var error=Validate(request);if(error is not null)return BadRequest(new{message=error});var premise=await repository.SaveAsync(id,request,user.UserId);return premise is null?NotFound(new{message="The event premise could not be found."}):Ok(premise);}
    [HttpPost("{id:int}/disable")] public async Task<ActionResult> Disable(int id){var user=GetUser();if(user is null)return Forbidden();if(id<=0)return BadRequest(new{message="Select a valid event premise."});return await repository.DisableAsync(id,user.UserId)?Ok(new{message="Event premise disabled."}):NotFound(new{message="The event premise could not be found."});}
    private BCLMSApi.Models.UserTokenPayload? GetUser(){var user=tokens.GetValidTokenPayload(Request.Headers.Authorization);return user is not null&&Groups.Any(g=>user.Groups.Contains(g,StringComparer.OrdinalIgnoreCase))?user:null;}
    private ActionResult Forbidden()=>StatusCode(403,new{message="Event premise management access is required."});
    private static string? Validate(EventPremiseSaveRequest r)=>string.IsNullOrWhiteSpace(r.Name)?"Premise name is required.":string.IsNullOrWhiteSpace(r.StreetNameAndNumber)?"Street name and number are required.":string.IsNullOrWhiteSpace(r.ContactDetails)?"Contact details are required.":r.Latitude==0||r.Longitude==0?"Pin the event premise location on the map.":null;
}
