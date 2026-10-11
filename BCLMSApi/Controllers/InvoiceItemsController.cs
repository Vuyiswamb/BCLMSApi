using BCLMSApi.Data;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/invoice-items")]
public class InvoiceItemsController(InvoiceItemRepository repository,IUserTokenService tokens):ControllerBase
{
    [HttpGet] public async Task<ActionResult<List<InvoiceItemResponse>>> Get(){var user=tokens.GetValidTokenPayload(Request.Headers.Authorization);if(user is null)return StatusCode(403,new{message="Login is required."});var items=await repository.GetAllAsync();return Ok(items.Where(x=>!user.IsCustomer||x.IsActive));}
    [HttpPost] public Task<ActionResult<InvoiceItemResponse>> Save(InvoiceItemSaveRequest request)=>SaveCore(null,request);
    [HttpPut("{id:int}")] public Task<ActionResult<InvoiceItemResponse>> Update(int id,InvoiceItemSaveRequest request)=>SaveCore(id,request);
    private async Task<ActionResult<InvoiceItemResponse>> SaveCore(int? id,InvoiceItemSaveRequest request){var user=tokens.GetValidTokenPayload(Request.Headers.Authorization);if(user is null||!user.Groups.Contains("Super User",StringComparer.OrdinalIgnoreCase))return StatusCode(403,new{message="Super User access is required."});Validate(request);var item=await repository.SaveAsync(id,request);return item is null?NotFound():Ok(item);}
    [HttpDelete("{id:int}")] public async Task<ActionResult> Delete(int id){var user=tokens.GetValidTokenPayload(Request.Headers.Authorization);if(user is null||!user.Groups.Contains("Super User",StringComparer.OrdinalIgnoreCase))return StatusCode(403,new{message="Super User access is required."});return await repository.DeleteAsync(id)?Ok(new{message="Invoice item deleted."}):NotFound();}
    private static void Validate(InvoiceItemSaveRequest r){if(string.IsNullOrWhiteSpace(r.ItemCode)||string.IsNullOrWhiteSpace(r.Description)||string.IsNullOrWhiteSpace(r.GlAccountNumber)||r.Amount<0)throw new ArgumentException("Item code, description, GL account and a valid amount are required.");}
}
