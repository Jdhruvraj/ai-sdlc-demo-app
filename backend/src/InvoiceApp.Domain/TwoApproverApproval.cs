using InvoiceApp.Domain;
using Microsoft.Extensions.Logging;

namespace InvoiceApp.Domain;

/// <summary>
/// Handles two-approver workflow for high-value invoices (>500,000).
/// First approver can be Manager or Director. Second MUST be Director.
/// </summary>
public class TwoApproverApproval
{
    private readonly IInvoiceRepository _repository;
    private readonly ILogger<TwoApproverApproval> _logger;

    public TwoApproverApproval(IInvoiceRepository repository, ILogger<TwoApproverApproval> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Submit first approver for high-value invoice (>500k)
    /// </summary>
    public ApprovalResult SubmitFirst(string invoiceId, string approver)
    {
        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        // Validate first approver role
        if (!IsValidRole(approver))
        {
            return new ApprovalResult 
            { 
                Success = false, 
                Error = "First approver must be a Manager or Director",
                Invoice = null
            };
        }

        invoice.Approver1 = approver;
        invoice.Approver1Date = DateTime.UtcNow;
        _repository.Update(invoice);

        return new ApprovalResult { Success = true, Error = null, Invoice = invoice };
    }

    /// <summary>
    /// Submit second approver for high-value invoice (>500k)
    /// </summary>
    public ApprovalResult SubmitSecond(string invoiceId, string approver)
    {
        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        // Must have first approver
        if (string.IsNullOrWhiteSpace(invoice.Approver1))
        {
            return new ApprovalResult 
            { 
                Success = false, 
                Error = "First approver not found. Submit first approval first.",
                Invoice = null
            };
        }

        // Second approver MUST be Director
        if (!IsDirector(approver))
        {
            return new ApprovalResult 
            { 
                Success = false, 
                Error = "Second approver on high-value invoice must be a Director",
                Invoice = null
            };
        }

        // Approvers must be distinct - same user cannot be both
        if (approver == invoice.Approver1)
        {
            return new ApprovalResult 
            { 
                Success = false, 
                Error = "Same user cannot be both approvers on the same invoice",
                Invoice = null
            };
        }

        // Complete approval
        invoice.Status = InvoiceStatus.Approved;
        invoice.Approver2 = approver;
        invoice.Approver2Date = DateTime.UtcNow;
        invoice.DecidedBy = $"{invoice.Approver1} + {approver}";
        invoice.DecidedAt = DateTime.UtcNow;
        _repository.Update(invoice);

        return new ApprovalResult { Success = true, Error = null, Invoice = invoice };
    }

    private Invoice Find(string invoiceId) =>
        _repository.GetById(invoiceId)
        ?? throw new KeyNotFoundException($"Invoice {invoiceId} not found");

    private void EnsurePending(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id} is {invoice.Status}; only pending invoices can be approved.");
        }
    }

    private bool IsValidRole(string approver) => 
        approver.Contains("manager", StringComparison.OrdinalIgnoreCase) || 
        approver.Contains("director", StringComparison.OrdinalIgnoreCase);

    private bool IsDirector(string approver) => 
        approver.Contains("director", StringComparison.OrdinalIgnoreCase);
}
