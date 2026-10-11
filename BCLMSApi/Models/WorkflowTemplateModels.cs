namespace BCLMSApi.Models;

public sealed class WorkflowTemplateResponse
{
    public int WorkflowTemplateId { get; set; }
    public string ApplicationType { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<WorkflowTemplateStepResponse> Steps { get; set; } = [];
}

public sealed class WorkflowTemplateStepResponse
{
    public int WorkflowTemplateStepId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string AssignedGroupName { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public string InitialStatus { get; set; } = "Pending";
    public bool IsRequired { get; set; }
}

public sealed class WorkflowTemplateSaveRequest
{
    public int? WorkflowTemplateId { get; set; }
    public string ApplicationType { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<WorkflowTemplateStepSaveRequest> Steps { get; set; } = [];
}

public sealed class WorkflowTemplateStepSaveRequest
{
    public string StepName { get; set; } = string.Empty;
    public string AssignedGroupName { get; set; } = string.Empty;
    public string InitialStatus { get; set; } = "Pending";
    public bool IsRequired { get; set; } = true;
}
