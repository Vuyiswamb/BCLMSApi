namespace BCLMSApi.Models;

public class AttachmentUploadResponse
{
    public int ApplicationDocumentId { get; set; }

    public int AttachmentTypeId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string Sha256Hash { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}
