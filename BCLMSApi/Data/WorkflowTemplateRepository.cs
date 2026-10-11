using System.Data;
using System.Text.Json;
using BCLMSApi.Models;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Data;

public sealed class WorkflowTemplateRepository(Datalayer datalayer)
{
    public async Task<List<WorkflowTemplateResponse>> GetAllAsync()
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_WorkflowTemplates_GetAll");
            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            var templates = new Dictionary<int, WorkflowTemplateResponse>();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(reader.GetOrdinal("WorkflowTemplateId"));
                if (!templates.TryGetValue(id, out var template))
                {
                    template = new WorkflowTemplateResponse
                    {
                        WorkflowTemplateId = id,
                        ApplicationType = reader.GetString(reader.GetOrdinal("ApplicationType")),
                        TemplateName = reader.GetString(reader.GetOrdinal("TemplateName")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                    };
                    templates.Add(id, template);
                }

                if (!reader.IsDBNull(reader.GetOrdinal("WorkflowTemplateStepId")))
                {
                    template.Steps.Add(MapStep(reader));
                }
            }

            return templates.Values.OrderBy(item => item.ApplicationType).ToList();
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException("Unable to load workflow templates.", ex);
        }
    }

    public async Task<List<string>> GetApplicationTypesAsync()
    {
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_WorkflowTemplates_GetApplicationTypes");
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        var types = new List<string>();
        while (await reader.ReadAsync()) types.Add(reader.GetString(0));
        return types;
    }

    public async Task<WorkflowTemplateResponse> SaveAsync(WorkflowTemplateSaveRequest request, int userId)
    {
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_WorkflowTemplates_Save");
        command.Parameters.AddWithValue("@WorkflowTemplateId", request.WorkflowTemplateId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ApplicationType", request.ApplicationType.Trim());
        command.Parameters.AddWithValue("@TemplateName", request.TemplateName.Trim());
        command.Parameters.AddWithValue("@IsActive", request.IsActive);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.Add("@StepsJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(
            request.Steps.Select((step, index) => new
            {
                StepName = step.StepName.Trim(),
                AssignedGroupName = step.AssignedGroupName.Trim(),
                SequenceNumber = index + 1,
                InitialStatus = string.IsNullOrWhiteSpace(step.InitialStatus) ? "Pending" : step.InitialStatus.Trim(),
                step.IsRequired
            }));

        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        var result = new WorkflowTemplateResponse();
        while (await reader.ReadAsync())
        {
            if (result.WorkflowTemplateId == 0)
            {
                result.WorkflowTemplateId = reader.GetInt32(reader.GetOrdinal("WorkflowTemplateId"));
                result.ApplicationType = reader.GetString(reader.GetOrdinal("ApplicationType"));
                result.TemplateName = reader.GetString(reader.GetOrdinal("TemplateName"));
                result.IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"));
            }
            result.Steps.Add(MapStep(reader));
        }

        return result;
    }

    private static WorkflowTemplateStepResponse MapStep(SqlDataReader reader) => new()
    {
        WorkflowTemplateStepId = reader.GetInt32(reader.GetOrdinal("WorkflowTemplateStepId")),
        StepName = reader.GetString(reader.GetOrdinal("StepName")),
        AssignedGroupName = reader.GetString(reader.GetOrdinal("AssignedGroupName")),
        SequenceNumber = reader.GetInt32(reader.GetOrdinal("SequenceNumber")),
        InitialStatus = reader.GetString(reader.GetOrdinal("InitialStatus")),
        IsRequired = reader.GetBoolean(reader.GetOrdinal("IsRequired"))
    };
}
