namespace BCLMSApi.Models;

public class AttachmentTypeRecord
{
    public int AttachmentTypeId { get; set; }

    public string TypeName { get; set; } = string.Empty;

    public bool IsRequired { get; set; }
}
