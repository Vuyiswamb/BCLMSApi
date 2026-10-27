using BCLMSApi.Data;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/attachment-types")]
public sealed class AttachmentTypesController(AttachmentTypeRepository repository, IUserTokenService tokens) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AttachmentTypeResponse>>> Get() => Ok(await repository.GetAsync(true));

    [HttpGet("admin")]
    public async Task<ActionResult<List<AttachmentTypeResponse>>> GetAdmin()
    {
        if (!tokens.IsSuperUserToken(Request.Headers.Authorization)) return Forbid();
        return Ok(await repository.GetAsync(false));
    }

    [HttpPost]
    public async Task<ActionResult<AttachmentTypeResponse>> Create(AttachmentTypeSaveRequest request) => await Save(null, request);

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AttachmentTypeResponse>> Update(int id, AttachmentTypeSaveRequest request) => await Save(id, request);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!tokens.IsSuperUserToken(Request.Headers.Authorization)) return Forbid();
        return await repository.DeleteAsync(id) ? Ok(new { message = "Attachment requirement removed." }) : NotFound();
    }

    private async Task<ActionResult<AttachmentTypeResponse>> Save(int? id, AttachmentTypeSaveRequest request)
    {
        if (!tokens.IsSuperUserToken(Request.Headers.Authorization)) return Forbid();
        if (string.IsNullOrWhiteSpace(request.TypeName) || request.LicenceTypes.Count == 0)
            return BadRequest(new { message = "Document name and at least one application type are required." });
        if (request.MaxFileSizeBytes <= 0 || string.IsNullOrWhiteSpace(request.AllowedExtension) || string.IsNullOrWhiteSpace(request.AllowedContentType))
            return BadRequest(new { message = "File size and file type settings are required." });
        return Ok(await repository.SaveAsync(id, request));
    }
}
