namespace InvoiceApp.Domain;

public class Invoice
{
    public string Id { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
    
    // Two-approver fields for high-value invoices (>500,000)
    public string? Approver1 { get; set; }
    public DateTime? Approver1Date { get; set; }
    public string? Approver2 { get; set; }
    public DateTime? Approver2Date { get; set; }
}
