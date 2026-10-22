using BCLMSApi.Data;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/lookups")]
public sealed class ApplicationLookupsController(ApplicationLookupRepository repository, IUserTokenService tokens) : ControllerBase
{
    private static readonly string[] AllowedTypes = ["BusinessType", "PerishableGoods", "PrePackedPerishableGoods"];

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<ApplicationLookupResponse>>> Get(string? type = null)
    {
        if (type is not null && !AllowedTypes.Contains(type, StringComparer.OrdinalIgnoreCase)) return BadRequest(new { message = "Unknown lookup type." });
        return Ok(await repository.GetAsync(type, activeOnly: true));
    }

    [HttpGet("admin")]
    public async Task<ActionResult<List<ApplicationLookupResponse>>> GetAdmin()
    {
        if (!tokens.IsSuperUserToken(Request.Headers.Authorization)) return Forbid();
        return Ok(await repository.GetAsync(activeOnly: false));
    }

    [HttpPost]
    public async Task<ActionResult<ApplicationLookupResponse>> Create(ApplicationLookupSaveRequest request)
    {
        if (!tokens.IsSuperUserToken(Request.Headers.Authorization)) return Forbid();
        if (!AllowedTypes.Contains(request.LookupType, StringComparer.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(request.LookupValue)) return BadRequest(new { message = "Select a valid lookup type and enter a value." });
        return Ok(await repository.SaveAsync(null, request));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApplicationLookupResponse>> Update(int id, ApplicationLookupSaveRequest request)
    {
        if (!tokens.IsSuperUserToken(Request.Headers.Authorization)) return Forbid();
        if (!AllowedTypes.Contains(request.LookupType, StringComparer.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(request.LookupValue)) return BadRequest(new { message = "Select a valid lookup type and enter a value." });
        var result = await repository.SaveAsync(id, request);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!tokens.IsSuperUserToken(Request.Headers.Authorization)) return Forbid();
        await repository.DeleteAsync(id);
        return NoContent();
    }
}
