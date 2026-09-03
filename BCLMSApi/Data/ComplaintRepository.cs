using BCLMSApi.Models;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Data;

public class ComplaintRepository(Datalayer datalayer) : IComplaintRepository
{
    public async Task<ComplaintResponse> CreateComplaintAsync(int userId, ComplaintCreateRequest request)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Complaints_Create");
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Category", request.Category.Trim());
            command.Parameters.AddWithValue("@Subject", request.Subject.Trim());
            command.Parameters.AddWithValue("@Description", request.Description.Trim());

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("The complaint could not be created.");
            }

            return MapComplaint(reader);
        }
        catch (SqlException error)
        {
            throw CreateDatabaseException("submit complaint", error);
        }
    }

    public async Task<List<ComplaintResponse>> GetCustomerComplaintsAsync(int userId)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Complaints_GetForCustomer");
            command.Parameters.AddWithValue("@UserId", userId);

            return await ReadComplaintsAsync(command);
        }
        catch (SqlException error)
        {
            throw CreateDatabaseException("load your complaints", error);
        }
    }

    public async Task<List<ComplaintResponse>> GetInternalComplaintsAsync()
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Complaints_GetInternal");
            return await ReadComplaintsAsync(command);
        }
        catch (SqlException error)
        {
            throw CreateDatabaseException("load complaints", error);
        }
    }

    public async Task<ComplaintResponse?> UpdateComplaintStatusAsync(int complaintId, int updatedByUserId, ComplaintStatusUpdateRequest request)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Complaints_UpdateStatus");
            command.Parameters.AddWithValue("@ComplaintId", complaintId);
            command.Parameters.AddWithValue("@Status", request.Status.Trim());
            command.Parameters.AddWithValue("@OfficialResponse", string.IsNullOrWhiteSpace(request.OfficialResponse) ? DBNull.Value : request.OfficialResponse.Trim());
            command.Parameters.AddWithValue("@UpdatedByUserId", updatedByUserId);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapComplaint(reader) : null;
        }
        catch (SqlException error)
        {
            throw CreateDatabaseException("update complaint", error);
        }
    }

    public async Task<ComplaintResponse?> CancelComplaintAsync(int complaintId, int userId)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Complaints_CancelForCustomer");
            command.Parameters.AddWithValue("@ComplaintId", complaintId);
            command.Parameters.AddWithValue("@UserId", userId);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapComplaint(reader) : null;
        }
        catch (SqlException error)
        {
            throw CreateDatabaseException("cancel complaint", error);
        }
    }

    private static async Task<List<ComplaintResponse>> ReadComplaintsAsync(Microsoft.Data.SqlClient.SqlCommand command)
    {
        var complaints = new List<ComplaintResponse>();
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            complaints.Add(MapComplaint(reader));
        }

        return complaints;
    }

    private static ComplaintResponse MapComplaint(System.Data.IDataRecord reader)
    {
        return new ComplaintResponse
        {
            ComplaintId = reader.GetInt32(reader.GetOrdinal("ComplaintId")),
            ReferenceNumber = reader.GetString(reader.GetOrdinal("ReferenceNumber")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            CustomerName = reader.GetString(reader.GetOrdinal("CustomerName")),
            CustomerEmail = reader.GetString(reader.GetOrdinal("CustomerEmail")),
            Category = reader.GetString(reader.GetOrdinal("Category")),
            Subject = reader.GetString(reader.GetOrdinal("Subject")),
            Description = reader.GetString(reader.GetOrdinal("Description")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            OfficialResponse = reader.IsDBNull(reader.GetOrdinal("OfficialResponse")) ? null : reader.GetString(reader.GetOrdinal("OfficialResponse")),
            CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")).ToString("O"),
            UpdatedDate = reader.IsDBNull(reader.GetOrdinal("UpdatedDate")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedDate")).ToString("O")
        };
    }

    private static InvalidOperationException CreateDatabaseException(string action, SqlException error)
    {
        var detail = error.Number switch
        {
            208 => "The Complaints table is missing. Run Database/bclms_schema.sql on the target database.",
            2812 => "The complaints stored procedures are missing. Run Database/bclms_stored_procedures.sql on the target database.",
            4121 => "The complaint reference sequence is missing. Run Database/bclms_schema.sql on the target database.",
            547 => "The logged-in user was not found in the database, so the complaint cannot be linked to a customer.",
            _ => error.Message
        };

        return new InvalidOperationException($"Unable to {action}: {detail}", error);
    }
}
