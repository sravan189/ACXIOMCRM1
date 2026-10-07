using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;

namespace AcxiomCRM.Tests;

/// <summary>DashboardService: KPI counts and zero-filled status/stage distributions.</summary>
public sealed class DashboardServiceTests
{
    private static async Task SeedAsync(TestDb tdb)
    {
        await TestDb.SeedUsersAsync(tdb.Db, "u-1");
        var db = tdb.Db;
        var now = DateTime.UtcNow;

        db.Customers.AddRange(
            new Customer { CustomerCode = "CUST-0001", CustomerName = "C1", Email = "c1@x.in", Phone = "9811111111", Status = CustomerStatus.Active, CreatedDate = now, CreatedById = "u-1", AssignedToId = "u-1" },
            new Customer { CustomerCode = "CUST-0002", CustomerName = "C2", Email = "c2@x.in", Phone = "9811111112", Status = CustomerStatus.Inactive, CreatedDate = now, CreatedById = "u-1", AssignedToId = "u-1" });

        db.Leads.AddRange(
            new Lead { LeadCode = "LEAD-0001", LeadName = "L1", Status = LeadStatus.New, CreatedDate = now, CreatedById = "u-1", AssignedToId = "u-1" },
            new Lead { LeadCode = "LEAD-0002", LeadName = "L2", Status = LeadStatus.Contacted, CreatedDate = now, CreatedById = "u-1", AssignedToId = "u-1" },
            new Lead { LeadCode = "LEAD-0003", LeadName = "L3", Status = LeadStatus.Converted, CreatedDate = now, CreatedById = "u-1", AssignedToId = "u-1" });

        db.Opportunities.AddRange(
            new Opportunity { OpportunityName = "O1", Amount = 1000, Stage = OpportunityStage.Qualification, Probability = 25, ExpectedCloseDate = DateTime.Today.AddDays(30), CreatedDate = now, CreatedById = "u-1", AssignedToId = "u-1" },
            new Opportunity { OpportunityName = "O2", Amount = 5000, Stage = OpportunityStage.Won, Probability = 100, ExpectedCloseDate = DateTime.Today.AddDays(-2), CreatedDate = now, CreatedById = "u-1", AssignedToId = "u-1" });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetDashboardAsync_KpiCounts_MatchSeededData()
    {
        using var tdb = TestDb.Create();
        await SeedAsync(tdb);
        var svc = new DashboardService(tdb.Db);

        var vm = await svc.GetDashboardAsync("u-1", seeAll: true, from: null, to: null);

        Assert.Equal(2, vm.TotalCustomers);
        Assert.Equal(3, vm.TotalLeads);
        Assert.Equal(2, vm.OpenLeads); // Converted is closed
        Assert.Equal(2, vm.TotalOpportunities);
        Assert.Equal(1, vm.OpenOpportunities); // Won is closed
        Assert.Equal(1, vm.WonOpportunities);
        Assert.Equal(0, vm.LostOpportunities);
        Assert.Equal(1000, vm.TotalPipelineValue); // open opps only
    }

    [Fact]
    public async Task GetDashboardAsync_LeadStatusCounts_HasAllSixStatuses()
    {
        using var tdb = TestDb.Create();
        await SeedAsync(tdb);
        var svc = new DashboardService(tdb.Db);

        var vm = await svc.GetDashboardAsync("u-1", seeAll: true, from: null, to: null);

        var expected = new[] { "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost" };
        Assert.Equal(expected.Length, vm.LeadStatusCounts.Count);
        foreach (var name in expected)
            Assert.True(vm.LeadStatusCounts.ContainsKey(name), $"missing status {name}");

        Assert.Equal(1, vm.LeadStatusCounts["New"]);
        Assert.Equal(1, vm.LeadStatusCounts["Contacted"]);
        Assert.Equal(1, vm.LeadStatusCounts["Converted"]);
        Assert.Equal(0, vm.LeadStatusCounts["Qualified"]); // zero-filled
    }

    [Fact]
    public async Task GetDashboardAsync_OpportunityStageAmounts_HasAllFiveStages()
    {
        using var tdb = TestDb.Create();
        await SeedAsync(tdb);
        var svc = new DashboardService(tdb.Db);

        var vm = await svc.GetDashboardAsync("u-1", seeAll: true, from: null, to: null);

        var expected = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
        Assert.Equal(expected.Length, vm.OpportunityStageAmounts.Count);
        foreach (var name in expected)
            Assert.True(vm.OpportunityStageAmounts.ContainsKey(name), $"missing stage {name}");

        Assert.Equal(1000, vm.OpportunityStageAmounts["Qualification"]);
        Assert.Equal(5000, vm.OpportunityStageAmounts["Won"]);
        Assert.Equal(0, vm.OpportunityStageAmounts["Proposal"]); // zero-filled
    }
}
