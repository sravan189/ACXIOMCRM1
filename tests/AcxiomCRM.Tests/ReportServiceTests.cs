using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Tests;

/// <summary>ReportService: visibility scoping and audit-log ordering.</summary>
public sealed class ReportServiceTests
{
    private static async Task<TestDb> CreateDbAsync()
    {
        var tdb = TestDb.Create();
        await TestDb.SeedUsersAsync(tdb.Db, "sales-1", "mgr-1", "admin-1");
        return tdb;
    }

    [Fact]
    public async Task GetLeadReportAsync_AsSalesExecutive_ReturnsOnlyOwnLeads()
    {
        using var tdb = await CreateDbAsync();
        var now = DateTime.UtcNow;
        tdb.Db.Leads.AddRange(
            new Lead { LeadCode = "LEAD-0001", LeadName = "Own Lead", Status = LeadStatus.New, CreatedDate = now, CreatedById = "sales-1", AssignedToId = "sales-1" },
            new Lead { LeadCode = "LEAD-0002", LeadName = "Other Lead", Status = LeadStatus.New, CreatedDate = now, CreatedById = "mgr-1", AssignedToId = "mgr-1" });
        await tdb.Db.SaveChangesAsync();
        var svc = new ReportService(tdb.Db);

        var scoped = await svc.GetLeadReportAsync("sales-1", seeAll: false, new LeadReportFilters(), 1, 20);

        Assert.Equal(1, scoped.TotalCount);
        Assert.Single(scoped.Items);
        Assert.Equal("Own Lead", scoped.Items[0].LeadName);

        var all = await svc.GetLeadReportAsync("admin-1", seeAll: true, new LeadReportFilters(), 1, 20);
        Assert.Equal(2, all.TotalCount);
    }

    [Fact]
    public async Task GetAuditLogAsync_ReturnsRows_NewestFirst()
    {
        using var tdb = await CreateDbAsync();
        var now = DateTime.UtcNow;
        tdb.Db.AuditLogs.AddRange(
            new AuditLog { Action = "First", EntityName = "Lead", CreatedDate = now.AddHours(-2), UserName = "t" },
            new AuditLog { Action = "Second", EntityName = "Lead", CreatedDate = now.AddHours(-1), UserName = "t" },
            new AuditLog { Action = "Third", EntityName = "Customer", CreatedDate = now, UserName = "t" });
        await tdb.Db.SaveChangesAsync();
        var svc = new ReportService(tdb.Db);

        var result = await svc.GetAuditLogAsync(new AuditLogFilters(), 1, 20);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(new[] { "Third", "Second", "First" },
            result.Items.Select(r => r.Action).ToArray());
    }

    [Fact]
    public async Task GetAuditLogAsync_Filters_ByEntityName()
    {
        using var tdb = await CreateDbAsync();
        var audit = new AuditService(tdb.Db);
        await audit.LogAsync("Create Customer", entityName: "Customer", userName: "t");
        await audit.LogAsync("Create Lead", entityName: "Lead", userName: "t");
        var svc = new ReportService(tdb.Db);

        var result = await svc.GetAuditLogAsync(new AuditLogFilters { EntityName = "Lead" }, 1, 20);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Lead", result.Items[0].EntityName);
    }
}
