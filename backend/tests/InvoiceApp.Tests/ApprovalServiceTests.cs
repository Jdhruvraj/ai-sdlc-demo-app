using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalServiceTests
{
    private static Invoice InvoiceWith(string id, InvoiceStatus status) =>
        new() { Id = id, CustomerName = "Test School", Amount = 100m, Status = status };

    private static ApprovalService ServiceFor(params Invoice[] invoices) =>
        new(new FakeInvoiceRepository(invoices));

    [Fact]
    public void Submit_MovesDraftToPendingApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);

        var result = ServiceFor(invoice).Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Approved));

        Assert.Throws<InvalidOperationException>(() => service.Submit("INV-1"));
    }

    [Fact]
    public void Approve_SetsStatusApproverAndTimestamp()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        var service = new ApprovalService(new FakeInvoiceRepository(invoice));

        var result = service.Approve("INV-1", "demo.approver");

        Assert.True(result.Success);
        Assert.Equal(InvoiceStatus.Approved, invoice.Status);
        Assert.Equal("demo.approver", invoice.DecidedBy);
        Assert.NotNull(invoice.DecidedAt);
    }

    [Fact]
    public void Approve_RejectsInvoiceThatIsStillADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Draft));

        Assert.Throws<InvalidOperationException>(() => service.Approve("INV-1", "demo.approver"));
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var service = ServiceFor();

        Assert.Throws<KeyNotFoundException>(() => service.Approve("INV-404", "demo.approver"));
    }

    [Fact]
    public void Reject_RecordsReasonAndApprover()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        var service = new ApprovalService(new FakeInvoiceRepository(invoice));

        var result = service.Reject("INV-1", "demo.approver", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        Assert.Throws<ArgumentException>(() => service.Reject("INV-1", "demo.approver", "  "));
    }
}

public class ApprovalLimitsTests
{
    private static ApprovalService CreateService(decimal amount, InvoiceStatus status) =>
        new(new FakeInvoiceRepository(new Invoice 
        { 
            Id = "INV-1", 
            CustomerName = "Test School", 
            Amount = amount, 
            Status = status 
        }));

    [Fact]
    public void Approve_Manager_CanApproveUpTo50K()
    {
        var service = CreateService(49_999m, InvoiceStatus.PendingApproval);

        var result = service.Approve("INV-1", "demo.manager");

        Assert.True(result.Success);
    }

    [Fact]
    public void Approve_Manager_CannotApproveOver50K()
    {
        var service = CreateService(50_001m, InvoiceStatus.PendingApproval);

        var result = service.Approve("INV-1", "demo.manager");

        Assert.False(result.Success);
        Assert.Contains("exceeds Manager limit", result.Error!);
    }

    [Fact]
    public void Approve_Manager_AtExactLimit_50K_Succeeds()
    {
        var service = CreateService(50_000m, InvoiceStatus.PendingApproval);

        var result = service.Approve("INV-1", "demo.manager");

        Assert.True(result.Success);
    }

    [Fact]
    public void Approve_Director_CanApproveUpTo500K()
    {
        var service = CreateService(499_999m, InvoiceStatus.PendingApproval);

        var result = service.Approve("INV-1", "demo.director");

        Assert.True(result.Success);
    }

    [Fact]
    public void Approve_Director_CannotApproveOver500K()
    {
        var service = CreateService(500_001m, InvoiceStatus.PendingApproval);

        var result = service.Approve("INV-1", "demo.director");

        Assert.False(result.Success);
        Assert.Contains("exceeds Director limit", result.Error!);
    }

    [Fact]
    public void Approve_Director_AtExactLimit_500K_Succeeds()
    {
        var service = CreateService(500_000m, InvoiceStatus.PendingApproval);

        var result = service.Approve("INV-1", "demo.director");

        Assert.True(result.Success);
    }

    [Fact]
    public void Approve_Manager_AttemptsOver500K_Rejected()
    {
        var service = CreateService(600_000m, InvoiceStatus.PendingApproval);

        var result = service.Approve("INV-1", "demo.manager");

        Assert.False(result.Success);
        Assert.Contains("exceeds Manager limit", result.Error!);
    }
}

public class TwoApproverApprovalTests
{
    private static Invoice HighValueInvoice(string id, InvoiceStatus status) =>
        new() { Id = id, CustomerName = "Test School", Amount = 600_000m, Status = status };

    [Fact]
    public void SubmitFirst_Manager_CanApprove600K()
    {
        var invoice = HighValueInvoice("INV-1", InvoiceStatus.PendingApproval);
        var service = new TwoApproverApproval(new FakeInvoiceRepository(invoice));

        var result = service.SubmitFirst("INV-1", "demo.manager");

        Assert.True(result.Success);
        Assert.Equal("demo.manager", invoice.Approver1);
    }

    [Fact]
    public void SubmitFirst_Director_CanApprove600K()
    {
        var invoice = HighValueInvoice("INV-1", InvoiceStatus.PendingApproval);
        var service = new TwoApproverApproval(new FakeInvoiceRepository(invoice));

        var result = service.SubmitFirst("INV-1", "demo.director");

        Assert.True(result.Success);
        Assert.Equal("demo.director", invoice.Approver1);
    }

    [Fact]
    public void SubmitSecond_MustBeDirector()
    {
        var invoice = HighValueInvoice("INV-1", InvoiceStatus.PendingApproval);
        
        _ = new TwoApproverApproval(new FakeInvoiceRepository(invoice))
            .SubmitFirst("INV-1", "demo.manager");

        var service = new TwoApproverApproval(new FakeInvoiceRepository(invoice));

        // Second approval by Manager (should fail)
        var result = service.SubmitSecond("INV-1", "demo.manager");

        Assert.False(result.Success);
        Assert.Contains("must be a Director", result.Error!);
        Assert.Null(invoice.Approver2); // Not updated
    }

    [Fact]
    public void SubmitSecond_SameUser_Rejected()
    {
        var invoice = HighValueInvoice("INV-1", InvoiceStatus.PendingApproval);
        
        _ = new TwoApproverApproval(new FakeInvoiceRepository(invoice))
            .SubmitFirst("INV-1", "demo.director");

        var service = new TwoApproverApproval(new FakeInvoiceRepository(invoice));

        // Same user as second approver (should fail)
        var result = service.SubmitSecond("INV-1", "demo.director");

        Assert.False(result.Success);
        Assert.Contains("Same user cannot be both approvers", result.Error!);
    }

    [Fact]
    public void SubmitSecond_FirstApproverNotSet_Rejected()
    {
        var invoice = HighValueInvoice("INV-1", InvoiceStatus.PendingApproval);
        
        // Clear first approver to simulate missing state
        invoice.Approver1 = null;
        _repository.Update(invoice);

        var service = new TwoApproverApproval(new FakeInvoiceRepository(invoice));

        var result = service.SubmitSecond("INV-1", "demo.director");

        Assert.False(result.Success);
        Assert.Contains("First approver not found", result.Error!);
    }

    [Fact]
    public void Complete_TwoDistinctApprovers_Success()
    {
        var invoice = HighValueInvoice("INV-1", InvoiceStatus.PendingApproval);
        
        var service = new TwoApproverApproval(new FakeInvoiceRepository(invoice));

        _ = service.SubmitFirst("INV-1", "demo.manager");
        var result = service.SubmitSecond("INV-1", "demo.director");

        Assert.True(result.Success);
        Assert.Equal(InvoiceStatus.Approved, invoice.Status);
        Assert.Equal("demo.manager", invoice.Approver1);
        Assert.Equal("demo.director", invoice.Approver2);
    }

    [Fact]
    public void Complete_ManagerAsSecond_ApproverRejected()
    {
        var invoice = HighValueInvoice("INV-1", InvoiceStatus.PendingApproval);
        
        _ = new TwoApproverApproval(new FakeInvoiceRepository(invoice))
            .SubmitFirst("INV-1", "demo.manager");

        var service = new TwoApproverApproval(new FakeInvoiceRepository(invoice));

        // Manager cannot be second approver
        var result = service.SubmitSecond("INV-1", "demo.manager");

        Assert.False(result.Success);
        Assert.Null(invoice.Approver2);
    }

    [Fact]
    public void Complete_BothDirectors_Success()
    {
        var invoice = HighValueInvoice("INV-1", InvoiceStatus.PendingApproval);
        
        _ = new TwoApproverApproval(new FakeInvoiceRepository(invoice))
            .SubmitFirst("INV-1", "demo.director");

        var service = new TwoApproverApproval(new FakeInvoiceRepository(invoice));

        // Different Director as second - success
        var result = service.SubmitSecond("INV-1", "demo.another-director");

        Assert.True(result.Success);
    }
}
