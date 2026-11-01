using InvoiceApp.Api.Auth;
using InvoiceApp.Domain;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireRole("Approver", "Admin")]
public class ApprovalsController : ApiControllerBase
{
    private readonly ApprovalService _approvals;
    private readonly TwoApproverApproval _twoApprovals;

    public ApprovalsController(ApprovalService approvals, TwoApproverApproval twoApprovals)
    {
        _approvals = approvals;
        _twoApprovals = twoApprovals;
    }

    public record RejectRequest(string Reason);

    [HttpPost("{invoiceId}/approve")]
    public ActionResult<Invoice> Approve(string invoiceId) =>
        Handle(() => _approvals.Approve(invoiceId, HttpContext.GetSession().Username));

    [HttpPost("{invoiceId}/reject")]
    public ActionResult<Invoice> Reject(string invoiceId, [FromBody] RejectRequest request) =>
        Handle(() => _approvals.Reject(invoiceId, HttpContext.GetSession().Username, request.Reason));

    /// <summary>
    /// Two-approver workflow for high-value invoices (>500k)
    /// </summary>
    public record TwoApproverRequest {
        public string InvoiceId { get; set; } = string.Empty;
        public string Approver { get; set; } = string.Empty;
        public bool IsSecondApprover { get; set; }
    }

    [HttpPost("{invoiceId}/approve/two")]
    public ActionResult<Invoice> ApproveTwo(string invoiceId, [FromBody] TwoApproverRequest request)
    {
        if (request.IsSecondApprover)
        {
            var result = _twoApprovals.SubmitSecond(invoiceId, request.Approver);
            return result.Success ? Ok(result.Invoice!) : BadRequest(new { error = result.Error });
        }

        var result = _twoApprovals.SubmitFirst(invoiceId, request.Approver);
        return result.Success ? Ok(result.Invoice!) : BadRequest(new { error = result.Error });
    }
}
