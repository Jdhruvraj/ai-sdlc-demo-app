namespace InvoiceApp.Domain;

/// <summary>
/// Result of an approval operation.
/// </summary>
public record ApprovalResult {
    public bool Success { get; set; }
    public string? Error { get; set; }
    public Invoice? Invoice { get; set; }
}

/// <summary>
/// Moves an invoice through its lifecycle: Draft -> PendingApproval -> Approved or Rejected.
/// One invoice at a time, decided as a whole by a single approver.
/// </summary>
public class ApprovalService
{
    private readonly IInvoiceRepository _repository;

    public ApprovalService(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public Invoice Submit(string invoiceId)
    {
        var invoice = Find(invoiceId);

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Invoice {invoiceId} is {invoice.Status}; only drafts can be submitted.");
        }

        invoice.Status = InvoiceStatus.PendingApproval;
        _repository.Update(invoice);
        return invoice;
    }

    public ApprovalResult Approve(string invoiceId, string approver)
    {
        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        // Determine max approval amount based on role
        var maxAmount = GetMaxApprovalAmount(approver);

        if (invoice.Amount > maxAmount)
        {
            return new ApprovalResult 
            { 
                Success = false, 
                Error = $"Invoice amount ({invoice.Amount}) exceeds Manager limit ({maxAmount})",
                Invoice = null
            };
        }

        // Approve successfully
        invoice.Status = InvoiceStatus.Approved;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
        _repository.Update(invoice);

        return new ApprovalResult 
        { 
            Success = true, 
            Error = null, 
            Invoice = invoice 
        };
    }

    private decimal GetMaxApprovalAmount(string approver)
    {
        // Role detection based on username pattern
        if (approver.Contains("manager", StringComparison.OrdinalIgnoreCase))
            return 50_000m;
        
        if (approver.Contains("director", StringComparison.OrdinalIgnoreCase))
            return 500_000m;
        
        throw new ArgumentException($"Unknown role for user: {approver}", nameof(approver));
    }

    public Invoice Reject(string invoiceId, string approver, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reject an invoice.", nameof(reason));
        }

        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        invoice.Status = InvoiceStatus.Rejected;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
        invoice.DecisionNote = reason;
        _repository.Update(invoice);
        return invoice;
    }

    private Invoice Find(string invoiceId) =>
        _repository.GetById(invoiceId)
        ?? throw new KeyNotFoundException($"Invoice {invoiceId} was not found.");

    private static void EnsurePending(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id} is {invoice.Status}; only pending invoices can be decided.");
        }
    }
}
