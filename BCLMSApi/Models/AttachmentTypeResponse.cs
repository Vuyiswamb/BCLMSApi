namespace BCLMSApi.Models;

public class AttachmentTypeResponse
{
    public int AttachmentTypeId { get; set; }

    public string TypeName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsRequired { get; set; }

    public long MaxFileSizeBytes { get; set; }

    public string AllowedExtension { get; set; } = ".pdf";

    public string AllowedContentType { get; set; } = "application/pdf";
}
