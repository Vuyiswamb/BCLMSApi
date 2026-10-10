namespace BCLMSApi.Models;

public class InvoiceItemResponse
{
    public int InvoiceItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GlAccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsActive { get; set; }
}

public class InvoiceItemSaveRequest
{
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GlAccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsActive { get; set; } = true;
}
