using System.Text.Json;
using BCLMSApi.Data;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController(InvoiceRepository repository, IUserTokenService tokens) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult> Save(InvoiceRequest request)
    { if (tokens.GetValidTokenPayload(Request.Headers.Authorization) is null) return Unauthorized(); await repository.SaveAsync(request.ApplicationId,request.PaymentReference,request.GlAccountNumber,JsonSerializer.Serialize(request.Lines)); return Ok(); }
    [HttpGet("{applicationId:int}")]
    public async Task<ActionResult> Get(int applicationId)
    { if (tokens.GetValidTokenPayload(Request.Headers.Authorization) is null) return Unauthorized(); var invoice=await repository.GetAsync(applicationId); return invoice is null ? Ok(new {paymentReference="",glAccountNumber="",paymentStatus="Unavailable",lines=Array.Empty<object>()}) : Ok(invoice); }
    [HttpGet("{applicationId:int}/transactions")]
    public async Task<ActionResult> Transactions(int applicationId)
    { if (tokens.GetValidTokenPayload(Request.Headers.Authorization) is null) return Unauthorized(); return Ok(await repository.GetTransactionsAsync(applicationId)); }
    [HttpPost("{applicationId:int}/confirm-payment")]
    public async Task<ActionResult> Confirm(int applicationId)
    { var user=tokens.GetValidTokenPayload(Request.Headers.Authorization); if(user is null||user.IsCustomer)return Forbid(); return await repository.ConfirmAsync(applicationId,user.UserId)?Ok():NotFound(); }
}
public class InvoiceRequest{public int ApplicationId{get;set;}public string PaymentReference{get;set;}="";public string GlAccountNumber{get;set;}="";public List<InvoiceLineRequest> Lines{get;set;}=[];}
public class InvoiceLineRequest{public string ItemCode{get;set;}="";public string Description{get;set;}="";public decimal Amount{get;set;}}
