namespace BCLMSApi.Models;

public class AttachmentUploadRequest
{
    public int AttachmentTypeId { get; set; }

    public IFormFile? File { get; set; }

    public string? Remarks { get; set; }
}
