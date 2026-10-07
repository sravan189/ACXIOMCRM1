using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Tests;

/// <summary>FollowUpService: date rules, completion, rescheduling + audit trail.</summary>
public sealed class FollowUpServiceTests
{
    private static FollowUpService Service(TestDb tdb)
        => new(tdb.Db, new AuditService(tdb.Db));

    private static async Task<FollowUp> CreatePlannedAsync(TestDb tdb, DateTime date)
    {
        await TestDb.SeedUsersAsync(tdb.Db, "u-1");
        var svc = Service(tdb);
        return await svc.CreateAsync(new FollowUp
        {
            FollowUpDate = date,
            FollowUpType = FollowUpType.Call,
            Remarks = "Call the client",
            Status = FollowUpStatus.Planned,
        }, "u-1", seeAll: true, "tester");
    }

    [Fact]
    public async Task CreateAsync_PlannedFollowUpWithPastDate_IsRejected()
    {
        using var tdb = TestDb.Create();
        var svc = Service(tdb);

        var ex = await Assert.ThrowsAsync<CrmValidationException>(() =>
            svc.CreateAsync(new FollowUp
            {
                FollowUpDate = DateTime.Today.AddDays(-1), // yesterday
                Status = FollowUpStatus.Planned,
            }, "u-1", seeAll: true, "tester"));

        Assert.Contains(ex.Errors, e => e.Field == nameof(FollowUp.FollowUpDate));
    }

    [Fact]
    public async Task CompleteAsync_OnPlannedFollowUp_Works_AndIsAudited()
    {
        using var tdb = TestDb.Create();
        var svc = Service(tdb);
        var created = await CreatePlannedAsync(tdb, DateTime.Today.AddDays(2));

        var completed = await svc.CompleteAsync(created.FollowUpId, "u-1", seeAll: true, "tester");

        Assert.Equal(FollowUpStatus.Completed, completed.Status);
        Assert.True(await tdb.Db.AuditLogs.AnyAsync(
            a => a.Action == "Follow-up Completed" && a.RecordId == created.FollowUpId.ToString()));
    }

    [Fact]
    public async Task CompleteAsync_OnAlreadyCompletedFollowUp_IsRejected()
    {
        using var tdb = TestDb.Create();
        var svc = Service(tdb);
        var created = await CreatePlannedAsync(tdb, DateTime.Today.AddDays(2));
        await svc.CompleteAsync(created.FollowUpId, "u-1", seeAll: true, "tester");

        await Assert.ThrowsAsync<CrmValidationException>(() =>
            svc.CompleteAsync(created.FollowUpId, "u-1", seeAll: true, "tester"));
    }

    [Fact]
    public async Task RescheduleAsync_WithPastDate_IsRejected()
    {
        using var tdb = TestDb.Create();
        var svc = Service(tdb);
        var created = await CreatePlannedAsync(tdb, DateTime.Today.AddDays(2));

        var ex = await Assert.ThrowsAsync<CrmValidationException>(() =>
            svc.RescheduleAsync(created.FollowUpId, DateTime.Today.AddDays(-1),
                "u-1", seeAll: true, "tester"));

        Assert.Contains("cannot be earlier than today", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RescheduleAsync_LogsOldAndNewDates_InAudit()
    {
        using var tdb = TestDb.Create();
        var svc = Service(tdb);
        var oldDate = DateTime.Today.AddDays(2);
        var newDate = DateTime.Today.AddDays(9);
        var created = await CreatePlannedAsync(tdb, oldDate);

        var rescheduled = await svc.RescheduleAsync(created.FollowUpId, newDate,
            "u-1", seeAll: true, "tester");

        Assert.Equal(newDate.Date, rescheduled.FollowUpDate.Date);
        Assert.Equal(FollowUpStatus.Planned, rescheduled.Status);

        var log = await tdb.Db.AuditLogs.SingleAsync(
            a => a.Action == "Follow-up Rescheduled" && a.RecordId == created.FollowUpId.ToString());
        Assert.Contains(oldDate.ToString("yyyy-MM-dd"), log.OldValue ?? string.Empty);
        Assert.Contains("Planned", log.OldValue ?? string.Empty);
        Assert.Contains(newDate.ToString("yyyy-MM-dd"), log.NewValue ?? string.Empty);
    }
}
