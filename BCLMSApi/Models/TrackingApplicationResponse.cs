namespace BCLMSApi.Models;

public class TrackingApplicationResponse
{
    public int ApplicationId { get; set; }

    public string TrackingNumber { get; set; } = string.Empty;

    public string BusinessName { get; set; } = string.Empty;

    public string LicenceType { get; set; } = string.Empty;

    public string SubmittedDate { get; set; } = string.Empty;

    public string CurrentStage { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool ProofOfPaymentUploaded { get; set; }

    public List<TrackingWorkflowStepResponse> Steps { get; set; } = [];
}
