namespace BCLMSApi.Models;

public class InternalApplicationSummaryResponse
{
    public int ApplicationId { get; set; }

    public string TrackingNumber { get; set; } = string.Empty;

    public string ApplicantName { get; set; } = string.Empty;

    public string BusinessName { get; set; } = string.Empty;

    public string LicenceType { get; set; } = string.Empty;

    public DateTime? EventStartDate { get; set; }

    public DateTime? EventEndDate { get; set; }

    public FoodVendingDetails? FoodVendingDetails { get; set; }

    public string? TradeStandBusinessType { get; set; }

    public string CurrentStage { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime SubmittedDate { get; set; }

    public string? RegionName { get; set; }

    public string? AreaOrSuburb { get; set; }

    public string? AreaCategory { get; set; }

    public int AttachmentCount { get; set; }

    public bool ProofOfPaymentUploaded { get; set; }
}

public class InternalApplicationDetailResponse : InternalApplicationSummaryResponse
{
    public int? BusinessId { get; set; }

    public string IdOrPassportNumber { get; set; } = string.Empty;

    public string EmailAddress { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public string? RegistrationNumber { get; set; }

    public decimal? ApplicationFee { get; set; }

    public string? WardNumber { get; set; }

    public string PhysicalAddress { get; set; } = string.Empty;

    public string? PostalAddress { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? Notes { get; set; }

    public List<InternalApplicationWorkflowStepResponse> WorkflowSteps { get; set; } = [];

    public List<InternalApplicationAttachmentResponse> Attachments { get; set; } = [];
}

public class InternalApplicationWorkflowStepResponse
{
    public string StepName { get; set; } = string.Empty;

    public string GroupName { get; set; } = string.Empty;

    public int SequenceNumber { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime? StartedDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    public string? Remarks { get; set; }

    public int? ActionedByUserId { get; set; }

    public string? ActionedByDisplayName { get; set; }

    public DateTime? DecisionDate { get; set; }
}

public class WorkflowStepActionRequest
{
    public string Decision { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public string? RejectionReason { get; set; }

    public int? AttachmentTypeId { get; set; }

    public string? DocumentName { get; set; }

    public string? FileName { get; set; }

    public string? ContentType { get; set; }

    public string? FileBase64 { get; set; }

    public string? SignatureBase64 { get; set; }
}

public class InternalApplicationAttachmentResponse
{
    public int ApplicationDocumentId { get; set; }

    public int? AttachmentTypeId { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public string? OriginalFileName { get; set; }

    public string? ContentType { get; set; }

    public long? FileSizeBytes { get; set; }

    public DateTime? SubmittedDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Remarks { get; set; }
}

public class ApplicationDocumentFileResponse
{
    public int ApplicationDocumentId { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    public string? OriginalFileName { get; set; }

    public string? ContentType { get; set; }

    public byte[] FileContent { get; set; } = [];
}
