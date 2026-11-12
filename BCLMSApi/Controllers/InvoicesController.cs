using System.Text.Json;
using BCLMSApi.Data;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController(InvoiceRepository repository, IUserTokenService tokens, IFormalBusinessService applications, BankMePaymentService bankMe) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult> Save(InvoiceRequest request)
    {
        if (tokens.GetValidTokenPayload(Request.Headers.Authorization) is null) return Unauthorized();
        if (request.ApplicationId <= 0 || request.Lines.Count == 0)
        {
            return BadRequest(new { message = "A valid application and at least one invoice line are required." });
        }

        try
        {
            await repository.SaveAsync(request.ApplicationId, request.PaymentReference, request.GlAccountNumber, JsonSerializer.Serialize(request.Lines));
            return Ok();
        }
        catch (InvalidOperationException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }
    [HttpGet("{applicationId:int}")]
    public async Task<ActionResult> Get(int applicationId)
    { var user=tokens.GetValidTokenPayload(Request.Headers.Authorization); if(user is null)return Unauthorized(); if(user.IsCustomer && await applications.GetInternalApplicationDetailAsync(applicationId,user) is null)return NotFound(); var invoice=await repository.GetAsync(applicationId); return invoice is null ? Ok(new {paymentReference="",glAccountNumber="",paymentStatus="Unavailable",lines=Array.Empty<object>()}) : Ok(invoice); }

    [HttpPost("{applicationId:int}/bankme-token")]
    public async Task<ActionResult<BankMeTokenResponse>> CreateBankMeToken(int applicationId, CancellationToken cancellationToken)
    {
        var user = tokens.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null) return Unauthorized();
        if (!user.IsCustomer) return Forbid();
        var application = await applications.GetInternalApplicationDetailAsync(applicationId, user);
        var invoice = await repository.GetAsync(applicationId);
        if (application is null || invoice is null) return NotFound(new { message = "Application invoice not found." });

        try { return Ok(await bankMe.CreateTokenAsync(application, invoice, cancellationToken)); }
        catch (InvalidOperationException error) { return BadRequest(new { message = error.Message }); }
    }

    [HttpPost("bankme-webhook")]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult> BankMeWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        if (!bankMe.IsValidWebhook(rawBody, Request.Headers["X-Webhook-Signature"].FirstOrDefault())) return Unauthorized();
        BankMeWebhookPayload? payload;
        try { payload = JsonSerializer.Deserialize<BankMeWebhookPayload>(rawBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { return BadRequest(); }
        if (payload is null || string.IsNullOrWhiteSpace(payload.GroupReference) || string.IsNullOrWhiteSpace(payload.PaymentUuid) || string.IsNullOrWhiteSpace(payload.Status)) return BadRequest();
        await bankMe.CompleteWebhookAsync(payload.GroupReference, payload.PaymentUuid, payload.Status, payload.TotalAmount);
        return Ok();
    }
    [HttpGet("{applicationId:int}/transactions")]
    public async Task<ActionResult> Transactions(int applicationId)
    { if (tokens.GetValidTokenPayload(Request.Headers.Authorization) is null) return Unauthorized(); return Ok(await repository.GetTransactionsAsync(applicationId)); }
    [HttpPost("{applicationId:int}/confirm-payment")]
    public async Task<ActionResult> Confirm(int applicationId)
    { var user=tokens.GetValidTokenPayload(Request.Headers.Authorization); if(user is null||user.IsCustomer)return Forbid(); return await repository.ConfirmAsync(applicationId,user.UserId)?Ok():NotFound(); }
}
public class InvoiceRequest{public int ApplicationId{get;set;}public string PaymentReference{get;set;}="";public string GlAccountNumber{get;set;}="";public List<InvoiceLineRequest> Lines{get;set;}=[];}
public class InvoiceLineRequest{public string ItemCode{get;set;}="";public string Description{get;set;}="";public decimal Amount{get;set;}}
