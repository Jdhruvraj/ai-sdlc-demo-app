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
