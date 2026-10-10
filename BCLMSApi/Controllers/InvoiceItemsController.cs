using BCLMSApi.Data;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/invoice-items")]
public class InvoiceItemsController(Datalayer datalayer, IUserTokenService userTokenService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<InvoiceItemResponse>>> Get()
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null) return StatusCode(403, new { message = "Login is required." });
        return Ok((await ReadItemsAsync()).Where(item => !user.IsCustomer || item.IsActive));
    }

    [HttpPost]
    public async Task<ActionResult<InvoiceItemResponse>> Save(InvoiceItemSaveRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null || !user.Groups.Contains("Super User", StringComparer.OrdinalIgnoreCase)) return StatusCode(403, new { message = "Super User access is required." });
        Validate(request);
        await using var command = datalayer.CreateTextCommand("""
            INSERT dbo.InvoiceItems (ItemCode, Description, GlAccountNumber, Amount, IsActive)
            VALUES (@ItemCode, @Description, @GlAccountNumber, @Amount, @IsActive);
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """);
        command.Parameters.AddWithValue("@ItemCode", request.ItemCode.Trim()); command.Parameters.AddWithValue("@Description", request.Description.Trim()); command.Parameters.AddWithValue("@GlAccountNumber", request.GlAccountNumber.Trim()); command.Parameters.AddWithValue("@Amount", request.Amount); command.Parameters.AddWithValue("@IsActive", request.IsActive);
        await command.Connection!.OpenAsync(); var id = Convert.ToInt32(await command.ExecuteScalarAsync());
        return Ok((await ReadItemsAsync()).Single(item => item.InvoiceItemId == id));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<InvoiceItemResponse>> Update(int id, InvoiceItemSaveRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null || !user.Groups.Contains("Super User", StringComparer.OrdinalIgnoreCase)) return StatusCode(403, new { message = "Super User access is required." });
        Validate(request);
        await using var command = datalayer.CreateTextCommand("UPDATE dbo.InvoiceItems SET ItemCode=@ItemCode, Description=@Description, GlAccountNumber=@GlAccountNumber, Amount=@Amount, IsActive=@IsActive, ModifiedDate=SYSUTCDATETIME() WHERE InvoiceItemId=@Id;");
        command.Parameters.AddWithValue("@Id", id); command.Parameters.AddWithValue("@ItemCode", request.ItemCode.Trim()); command.Parameters.AddWithValue("@Description", request.Description.Trim()); command.Parameters.AddWithValue("@GlAccountNumber", request.GlAccountNumber.Trim()); command.Parameters.AddWithValue("@Amount", request.Amount); command.Parameters.AddWithValue("@IsActive", request.IsActive);
        await command.Connection!.OpenAsync(); if (await command.ExecuteNonQueryAsync() == 0) return NotFound();
        return Ok((await ReadItemsAsync()).Single(item => item.InvoiceItemId == id));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null || !user.Groups.Contains("Super User", StringComparer.OrdinalIgnoreCase)) return StatusCode(403, new { message = "Super User access is required." });
        await using var command = datalayer.CreateTextCommand("DELETE FROM dbo.InvoiceItems WHERE InvoiceItemId = @Id;");
        command.Parameters.AddWithValue("@Id", id);
        await command.Connection!.OpenAsync();
        return await command.ExecuteNonQueryAsync() > 0 ? Ok(new { message = "Invoice item deleted." }) : NotFound();
    }

    private async Task<List<InvoiceItemResponse>> ReadItemsAsync()
    {
        var results = new List<InvoiceItemResponse>(); await using var command = datalayer.CreateTextCommand("SELECT InvoiceItemId, ItemCode, Description, GlAccountNumber, Amount, IsActive FROM dbo.InvoiceItems ORDER BY ItemCode;"); await command.Connection!.OpenAsync(); await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) results.Add(new InvoiceItemResponse { InvoiceItemId=reader.GetInt32(0), ItemCode=reader.GetString(1), Description=reader.GetString(2), GlAccountNumber=reader.GetString(3), Amount=reader.GetDecimal(4), IsActive=reader.GetBoolean(5) }); return results;
    }
    private static void Validate(InvoiceItemSaveRequest request) { if (string.IsNullOrWhiteSpace(request.ItemCode) || string.IsNullOrWhiteSpace(request.Description) || string.IsNullOrWhiteSpace(request.GlAccountNumber) || request.Amount < 0) throw new ArgumentException("Item code, description, GL account and a valid amount are required."); }
}
