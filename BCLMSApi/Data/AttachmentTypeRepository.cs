using BCLMSApi.Models;

namespace BCLMSApi.Data;

public sealed class AttachmentTypeRepository(Datalayer data)
{
    public async Task<List<AttachmentTypeResponse>> GetAsync(bool activeOnly)
    {
        await using var command = data.CreateTextCommand($"""
            SELECT a.AttachmentTypeId, a.TypeName, a.Description, a.IsRequired,
                   a.MaxFileSizeBytes, a.AllowedExtension, a.AllowedContentType,
                   STRING_AGG(CASE WHEN m.ApplicationType IS NULL THEN NULL ELSE m.ApplicationType END, '|') AS LicenceTypes
            FROM dbo.AttachmentTypes a
            LEFT JOIN dbo.AttachmentTypeApplicationTypes m ON m.AttachmentTypeId = a.AttachmentTypeId
            WHERE {(activeOnly ? "a.IsActive = 1 AND" : string.Empty)}
                  a.TypeName <> 'Proof of payment'
            GROUP BY a.AttachmentTypeId, a.TypeName, a.Description, a.IsRequired,
                     a.MaxFileSizeBytes, a.AllowedExtension, a.AllowedContentType
            ORDER BY a.TypeName;
            """);
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<AttachmentTypeResponse>();
        while (await reader.ReadAsync())
        {
            var assignments = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
            result.Add(new AttachmentTypeResponse
            {
                AttachmentTypeId = reader.GetInt32(0),
                TypeName = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                IsRequired = reader.GetBoolean(3),
                MaxFileSizeBytes = reader.GetInt64(4),
                AllowedExtension = reader.GetString(5),
                AllowedContentType = reader.GetString(6),
                LicenceTypes = assignments.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            });
        }
        return result;
    }

    public async Task<AttachmentTypeResponse> SaveAsync(int? id, AttachmentTypeSaveRequest request)
    {
        await using var connection = data.CreateTextCommand("SELECT 1").Connection!;
        await connection.OpenAsync();
        await using var transaction = (Microsoft.Data.SqlClient.SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = id is null
                ? """INSERT dbo.AttachmentTypes (TypeName, Description, IsRequired, MaxFileSizeBytes, AllowedExtension, AllowedContentType, IsActive) OUTPUT INSERTED.AttachmentTypeId VALUES (@TypeName,@Description,@IsRequired,@MaxFileSizeBytes,@AllowedExtension,@AllowedContentType,1);"""
                : """UPDATE dbo.AttachmentTypes SET TypeName=@TypeName, Description=@Description, IsRequired=@IsRequired, MaxFileSizeBytes=@MaxFileSizeBytes, AllowedExtension=@AllowedExtension, AllowedContentType=@AllowedContentType, IsActive=1 WHERE AttachmentTypeId=@Id; SELECT @Id;""";
            AddParameters(command, request, id);
            var savedId = Convert.ToInt32(await command.ExecuteScalarAsync());

            await using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM dbo.AttachmentTypeApplicationTypes WHERE AttachmentTypeId=@Id;";
            delete.Parameters.AddWithValue("@Id", savedId);
            await delete.ExecuteNonQueryAsync();

            foreach (var licenceType in request.LicenceTypes.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await using var assignment = connection.CreateCommand();
                assignment.Transaction = transaction;
                assignment.CommandText = "INSERT dbo.AttachmentTypeApplicationTypes (AttachmentTypeId, ApplicationType) VALUES (@Id,@ApplicationType);";
                assignment.Parameters.AddWithValue("@Id", savedId);
                assignment.Parameters.AddWithValue("@ApplicationType", licenceType);
                await assignment.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return (await GetAsync(false)).Single(item => item.AttachmentTypeId == savedId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var command = data.CreateTextCommand("UPDATE dbo.AttachmentTypes SET IsActive=0 WHERE AttachmentTypeId=@Id;");
        command.Parameters.AddWithValue("@Id", id);
        await command.Connection!.OpenAsync();
        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static void AddParameters(System.Data.Common.DbCommand command, AttachmentTypeSaveRequest request, int? id)
    {
        command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@Id", (object?)id ?? DBNull.Value));
        command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@TypeName", request.TypeName.Trim()));
        command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@Description", (object?)request.Description?.Trim() ?? DBNull.Value));
        command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@IsRequired", request.IsRequired));
        command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@MaxFileSizeBytes", request.MaxFileSizeBytes));
        command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@AllowedExtension", request.AllowedExtension.Trim()));
        command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@AllowedContentType", request.AllowedContentType.Trim()));
    }
}
