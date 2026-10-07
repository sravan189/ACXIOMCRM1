using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;

namespace AcxiomCRM.Tests;

/// <summary>OpportunityService: create validation, stage transitions, pipeline.</summary>
public sealed class OpportunityServiceTests
{
    private static Opportunity NewOpportunity(string name = "Big Deal") => new()
    {
        OpportunityName = name,
        Amount = 250000,
        Stage = OpportunityStage.Qualification,
        Probability = 25,
        ExpectedCloseDate = DateTime.Today.AddDays(45),
        Source = "Website",
    };

    private static OpportunityService Service(TestDb tdb)
        => new(tdb.Db, new AuditService(tdb.Db));

    private static async Task<TestDb> CreateDbAsync()
    {
        var tdb = TestDb.Create();
        await TestDb.SeedUsersAsync(tdb.Db, "u-1");
        return tdb;
    }

    [Fact]
    public async Task CreateAsync_AmountZeroOnOpenStage_IsRejected()
    {
        using var tdb = await CreateDbAsync();
        var svc = Service(tdb);

        var opp = NewOpportunity();
        opp.Amount = 0;

        var ex = await Assert.ThrowsAsync<CrmValidationException>(
            () => svc.CreateAsync(opp, "u-1", seeAll: true, "tester"));
        Assert.Contains(ex.Errors, e => e.Field == nameof(Opportunity.Amount));
    }

    [Fact]
    public async Task CreateAsync_Probability101_IsRejected()
    {
        using var tdb = await CreateDbAsync();
        var svc = Service(tdb);

        var opp = NewOpportunity();
        opp.Probability = 101;

        var ex = await Assert.ThrowsAsync<CrmValidationException>(
            () => svc.CreateAsync(opp, "u-1", seeAll: true, "tester"));
        Assert.Contains(ex.Errors, e => e.Field == nameof(Opportunity.Probability));
    }

    [Fact]
    public async Task CreateAsync_PastExpectedCloseDate_IsRejected()
    {
        using var tdb = await CreateDbAsync();
        var svc = Service(tdb);

        var opp = NewOpportunity();
        opp.ExpectedCloseDate = DateTime.Today.AddDays(-1);

        var ex = await Assert.ThrowsAsync<CrmValidationException>(
            () => svc.CreateAsync(opp, "u-1", seeAll: true, "tester"));
        Assert.Contains(ex.Errors, e => e.Field == nameof(Opportunity.ExpectedCloseDate));
    }

    [Fact]
    public async Task CreateAsync_ValidOpportunity_IsAudited()
    {
        using var tdb = await CreateDbAsync();
        var svc = Service(tdb);

        var created = await svc.CreateAsync(NewOpportunity("Audited Deal"), "u-1", seeAll: true, "tester");

        Assert.True(created.OpportunityId > 0);
        Assert.True(await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
            tdb.Db.AuditLogs,
            a => a.Action == "Create Opportunity" && a.RecordId == created.OpportunityId.ToString()));
    }

    [Fact]
    public async Task ChangeStageAsync_QualificationToProposal_IsAllowed()
    {
        using var tdb = await CreateDbAsync();
        var svc = Service(tdb);
        var created = await svc.CreateAsync(NewOpportunity(), "u-1", seeAll: true, "tester");

        var moved = await svc.ChangeStageAsync(created.OpportunityId, OpportunityStage.Proposal,
            "u-1", seeAll: true, "tester");

        Assert.Equal(OpportunityStage.Proposal, moved.Stage);
    }

    [Fact]
    public async Task ChangeStageAsync_QualificationToWon_IsRejected_MustPassThroughStages()
    {
        using var tdb = await CreateDbAsync();
        var svc = Service(tdb);
        var created = await svc.CreateAsync(NewOpportunity(), "u-1", seeAll: true, "tester");

        // Per AllowedTransitions, Qualification may only go to Proposal.
        var ex = await Assert.ThrowsAsync<CrmValidationException>(
            () => svc.ChangeStageAsync(created.OpportunityId, OpportunityStage.Won,
                "u-1", seeAll: true, "tester"));

        Assert.Contains("Cannot move an opportunity from Qualification to Won", ex.Message);
    }

    [Fact]
    public async Task GetPipelineAsync_ReturnsAllFiveStages_WithCorrectCounts()
    {
        using var tdb = await CreateDbAsync();
        var svc = Service(tdb);

        // 2 in Qualification, 1 advanced to Negotiation, the rest empty.
        await svc.CreateAsync(NewOpportunity("Q1"), "u-1", seeAll: true, "t");
        await svc.CreateAsync(NewOpportunity("Q2"), "u-1", seeAll: true, "t");
        var third = await svc.CreateAsync(NewOpportunity("N1"), "u-1", seeAll: true, "t");
        await svc.ChangeStageAsync(third.OpportunityId, OpportunityStage.Proposal, "u-1", seeAll: true, "t");
        await svc.ChangeStageAsync(third.OpportunityId, OpportunityStage.Negotiation, "u-1", seeAll: true, "t");

        var stages = (await svc.GetPipelineAsync("u-1", seeAll: true)).ToDictionary(s => s.Stage);

        Assert.Equal(5, stages.Count);
        Assert.Equal(Enum.GetValues<OpportunityStage>().Length, stages.Count);
        Assert.Equal(2, stages[OpportunityStage.Qualification].Count);
        Assert.Equal(0, stages[OpportunityStage.Proposal].Count);
        Assert.Equal(1, stages[OpportunityStage.Negotiation].Count);
        Assert.Equal(0, stages[OpportunityStage.Won].Count);
        Assert.Equal(0, stages[OpportunityStage.Lost].Count);
        Assert.Equal(500000, stages[OpportunityStage.Qualification].TotalAmount);
    }
}
