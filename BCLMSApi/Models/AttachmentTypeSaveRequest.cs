namespace BCLMSApi.Models;

public class AttachmentTypeSaveRequest
{
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsRequired { get; set; } = true;
    public List<string> LicenceTypes { get; set; } = [];
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
    public string AllowedExtension { get; set; } = ".pdf";
    public string AllowedContentType { get; set; } = "application/pdf";
}
