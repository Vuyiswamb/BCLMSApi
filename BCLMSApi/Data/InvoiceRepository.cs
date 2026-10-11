using BCLMSApi.Models;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Data;

public sealed record InvoiceLine(string ItemCode, string Description, decimal Amount);

public sealed record InvoiceData(
    string PaymentReference,
    string GlAccountNumber,
    string PaymentStatus,
    List<InvoiceLine> Lines);

public sealed record PaymentTransaction(
    string PaymentReference,
    decimal Amount,
    DateTime PaymentMonth,
    string PaymentStatus,
    DateTime RecordedDate);

public class InvoiceRepository(Datalayer datalayer)
{
    public async Task SaveAsync(int applicationId, string reference, string gl, string linesJson)
    {
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Invoices_Save");
        command.Parameters.AddWithValue("@ApplicationId", applicationId);
        command.Parameters.AddWithValue("@PaymentReference", reference);
        command.Parameters.AddWithValue("@GlAccountNumber", gl);
        command.Parameters.AddWithValue("@LinesJson", linesJson);
        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    public async Task<InvoiceData?> GetAsync(int applicationId)
    {
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Invoices_Get");
        command.Parameters.AddWithValue("@ApplicationId", applicationId);
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        string? paymentReference = null;
        string? glAccountNumber = null;
        string? paymentStatus = null;
        var lines = new List<InvoiceLine>();

        while (await reader.ReadAsync())
        {
            paymentReference ??= reader.GetString(0);
            glAccountNumber ??= reader.GetString(1);
            paymentStatus ??= reader.GetString(2);
            lines.Add(new InvoiceLine(reader.GetString(3), reader.GetString(4), reader.GetDecimal(5)));
        }

        return paymentReference is null
            ? null
            : new InvoiceData(paymentReference, glAccountNumber!, paymentStatus!, lines);
    }

    public async Task<List<PaymentTransaction>> GetTransactionsAsync(int applicationId)
    {
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Invoices_GetTransactions");
        command.Parameters.AddWithValue("@ApplicationId", applicationId);
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        var transactions = new List<PaymentTransaction>();
        while (await reader.ReadAsync())
        {
            transactions.Add(new PaymentTransaction(
                reader.GetString(0),
                reader.GetDecimal(1),
                reader.GetDateTime(2),
                reader.GetString(3),
                reader.GetDateTime(4)));
        }

        return transactions;
    }

    public async Task<bool> ConfirmAsync(int applicationId, int userId)
    {
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Invoices_ConfirmPayment");
        command.Parameters.AddWithValue("@ApplicationId", applicationId);
        command.Parameters.AddWithValue("@UserId", userId);
        await command.Connection!.OpenAsync();
        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }
}

public class InvoiceItemRepository(Datalayer datalayer)
{
    public async Task<List<InvoiceItemResponse>> GetAllAsync()
    {
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_InvoiceItems_GetAll");
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        var items = new List<InvoiceItemResponse>();
        while (await reader.ReadAsync())
        {
            items.Add(new InvoiceItemResponse
            {
                InvoiceItemId = reader.GetInt32(0),
                ItemCode = reader.GetString(1),
                Description = reader.GetString(2),
                GlAccountNumber = reader.GetString(3),
                Amount = reader.GetDecimal(4),
                IsActive = reader.GetBoolean(5)
            });
        }

        return items;
    }

    public async Task<InvoiceItemResponse?> SaveAsync(int? id, InvoiceItemSaveRequest request)
    {
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_InvoiceItems_Save");
        command.Parameters.AddWithValue("@InvoiceItemId", (object?)id ?? DBNull.Value);
        command.Parameters.AddWithValue("@ItemCode", request.ItemCode.Trim());
        command.Parameters.AddWithValue("@Description", request.Description.Trim());
        command.Parameters.AddWithValue("@GlAccountNumber", request.GlAccountNumber.Trim());
        command.Parameters.AddWithValue("@Amount", request.Amount);
        command.Parameters.AddWithValue("@IsActive", request.IsActive);
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        return await reader.ReadAsync()
            ? new InvoiceItemResponse
            {
                InvoiceItemId = reader.GetInt32(0),
                ItemCode = reader.GetString(1),
                Description = reader.GetString(2),
                GlAccountNumber = reader.GetString(3),
                Amount = reader.GetDecimal(4),
                IsActive = reader.GetBoolean(5)
            }
            : null;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_InvoiceItems_Delete");
        command.Parameters.AddWithValue("@InvoiceItemId", id);
        await command.Connection!.OpenAsync();
        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }
}
