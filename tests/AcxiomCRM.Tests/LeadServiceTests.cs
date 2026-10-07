using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;

namespace AcxiomCRM.Tests;

/// <summary>LeadService: status state machine, conversion workflow, assignment.</summary>
public sealed class LeadServiceTests
{
    private static Lead NewLead(string name, string email, string phone) => new()
    {
        LeadName = name,
        Email = email,
        Phone = phone,
        CompanyName = "Test Co",
        Source = "Website",
        Priority = LeadPriority.High,
        ExpectedValue = 100000,
    };

    private static LeadService Service(TestDb tdb)
        => new(tdb.Db, new AuditService(tdb.Db), TestDb.NewUserManager(tdb.Db));

    private static int _leadCounter;

    private static async Task<Lead> CreateLeadAsync(TestDb tdb, string suffix = "a")
    {
        await TestDb.SeedUsersAsync(tdb.Db, "u-admin", "u-1");
        var n = Interlocked.Increment(ref _leadCounter);
        var phone = $"98111{n:00000}"; // 10 digits, starts with 9
        var svc = Service(tdb);
        var (ok, error, lead) = await svc.CreateAsync(
            NewLead($"Lead {suffix}", $"lead{suffix}{n}@example.in", phone),
            "u-admin", "admin", null);
        Assert.True(ok, error);
        return lead!;
    }

    [Fact]
    public async Task ChangeStatusAsync_NewToContacted_IsAllowed()
    {
        using var tdb = TestDb.Create();
        var svc = Service(tdb);
        var lead = await CreateLeadAsync(tdb);

        var (ok, error, updated) = await svc.ChangeStatusAsync(lead.LeadId, LeadStatus.Contacted, "u-1", "tester", null);

        Assert.True(ok, error);
        Assert.Equal(LeadStatus.Contacted, updated!.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_NewToQualified_IsRejected()
    {
        using var tdb = TestDb.Create();
        var svc = Service(tdb);
        var lead = await CreateLeadAsync(tdb);

        var (ok, error, _) = await svc.ChangeStatusAsync(lead.LeadId, LeadStatus.Qualified, "u-1", "tester", null);

        Assert.False(ok);
        Assert.Contains("Cannot move lead from New to Qualified", error);
    }

    [Fact]
    public async Task ChangeStatusAsync_DirectToConverted_IsRejected_MustUseConvertAction()
    {
        using var tdb = TestDb.Create();
        var svc = Service(tdb);
        var lead = await CreateLeadAsync(tdb);

        var (ok, error, _) = await svc.ChangeStatusAsync(lead.LeadId, LeadStatus.Converted, "u-1", "tester", null);

        Assert.False(ok);
        Assert.Contains("only be marked Converted through the Convert action", error);
    }

    [Fact]
    public async Task ChangeStatusAsync_ConvertedIsTerminal_RejectsAnyMoveOut()
    {
        using var tdb = TestDb.Create();
        var svc = Service(tdb);
        var lead = await CreateLeadAsync(tdb);

        // Walk to Qualified, then convert -> lead becomes Converted (terminal).
        Assert.True((await svc.ChangeStatusAsync(lead.LeadId, LeadStatus.Contacted, "u-1", "t", null)).ok);
        Assert.True((await svc.ChangeStatusAsync(lead.LeadId, LeadStatus.Qualified, "u-1", "t", null)).ok);
        var (convOk, convError, _, _) = await svc.ConvertAsync(
            lead.LeadId, createOpportunity: false, opportunityName: null,
            opportunityAmount: 0, probability: 0, expectedCloseDate: null,
            "u-1", "t", null);
        Assert.True(convOk, convError);

        foreach (var target in new[] { LeadStatus.New, LeadStatus.Contacted, LeadStatus.Qualified, LeadStatus.Lost })
        {
            var (ok, error, _) = await svc.ChangeStatusAsync(lead.LeadId, target, "u-1", "t", null);
            Assert.False(ok);
            Assert.Contains($"Cannot move lead from Converted to {target}", error);
        }
    }

    [Fact]
    public async Task ConvertAsync_QualifiedLead_CreatesCustomerAndOpportunity_SetsConverted()
    {
        using var tdb = TestDb.Create();
        var svc = Service(tdb);
        var lead = await CreateLeadAsync(tdb, "conv");

        Assert.True((await svc.ChangeStatusAsync(lead.LeadId, LeadStatus.Contacted, "u-1", "t", null)).ok);
        Assert.True((await svc.ChangeStatusAsync(lead.LeadId, LeadStatus.Qualified, "u-1", "t", null)).ok);

        var closeDate = DateTime.Today.AddDays(30);
        var (ok, error, customer, opportunity) = await svc.ConvertAsync(
            lead.LeadId, createOpportunity: true, opportunityName: "Conversion Deal",
            opportunityAmount: 500000, probability: 60, expectedCloseDate: closeDate,
            "u-1", "tester", null);

        Assert.True(ok, error);
        Assert.NotNull(customer);
        Assert.StartsWith("CUST-", customer.CustomerCode);
        Assert.Equal(lead.LeadName, customer.CustomerName);
        Assert.Equal(lead.Email, customer.Email);

        Assert.NotNull(opportunity);
        Assert.Equal("Conversion Deal", opportunity.OpportunityName);
        Assert.Equal(customer.CustomerId, opportunity.CustomerId);
        Assert.Equal(OpportunityStage.Qualification, opportunity.Stage);

        var refreshed = await tdb.Db.Leads.FindAsync(lead.LeadId);
        Assert.Equal(LeadStatus.Converted, refreshed!.Status);
    }

    [Fact]
    public async Task ConvertAsync_NonQualifiedLead_IsRejected()
    {
        using var tdb = TestDb.Create();
        var svc = Service(tdb);
        var lead = await CreateLeadAsync(tdb, "nq"); // still New

        var (ok, error, customer, opportunity) = await svc.ConvertAsync(
            lead.LeadId, createOpportunity: false, opportunityName: null,
            opportunityAmount: 0, probability: 0, expectedCloseDate: null,
            "u-1", "t", null);

        Assert.False(ok);
        Assert.Contains("Only qualified leads can be converted", error);
        Assert.Null(customer);
        Assert.Null(opportunity);
    }

    [Fact]
    public async Task AssignAsync_ToSalesExecutive_Works_AndIsAudited()
    {
        using var tdb = TestDb.Create();
        var svc = Service(tdb);
        var lead = await CreateLeadAsync(tdb, "as");

        var exec = await TestDb.SeedUserWithRoleAsync(tdb.Db, "exec@example.in", RoleNames.SalesExecutive);

        var (ok, error, assigned) = await svc.AssignAsync(lead.LeadId, exec.Id, "u-admin", "admin", null);

        Assert.True(ok, error);
        Assert.Equal(exec.Id, assigned!.AssignedToId);
        Assert.True(await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
            tdb.Db.AuditLogs, a => a.Action == "Lead Assigned" && a.RecordId == lead.LeadId.ToString()));
    }

    [Fact]
    public async Task AssignAsync_ToUserWithoutCrmRole_IsRejected()
    {
        using var tdb = TestDb.Create();
        var svc = Service(tdb);
        var lead = await CreateLeadAsync(tdb, "nr");

        // User exists but has no SalesExecutive/Manager role.
        var plain = new ApplicationUser { UserName = "plain@example.in", Email = "plain@example.in", IsActive = true };
        tdb.Db.Users.Add(plain);
        await tdb.Db.SaveChangesAsync();

        var (ok, error, _) = await svc.AssignAsync(lead.LeadId, plain.Id, "u-admin", "admin", null);

        Assert.False(ok);
        Assert.Contains("only be assigned to a Sales Executive or Manager", error);
    }
}
