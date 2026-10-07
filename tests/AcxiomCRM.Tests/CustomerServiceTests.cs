using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Tests;

/// <summary>CustomerService: validation, duplicate rules, visibility scoping, soft delete, audit.</summary>
public sealed class CustomerServiceTests
{
    private static Customer NewCustomer(string name, string email, string phone,
        string? assignedToId = null, string? createdById = null) => new()
    {
        CustomerName = name,
        Email = email,
        Phone = phone,
        CompanyName = "Test Co",
        City = "Bengaluru",
        State = "Karnataka",
        AssignedToId = assignedToId,
        CreatedById = createdById,
    };

    private static async Task<TestDb> CreateDbAsync()
    {
        var tdb = TestDb.Create();
        await TestDb.SeedUsersAsync(tdb.Db, "u-admin", "sales-1", "mgr-1", "u-7");
        return tdb;
    }

    private static CustomerService Service(TestDb tdb)
        => new(tdb.Db, new AuditService(tdb.Db));

    [Fact]
    public async Task CreateAsync_RejectsDuplicateEmail()
    {
        using var tdb = await CreateDbAsync();
        var svc = Service(tdb);

        var first = await svc.CreateAsync(
            NewCustomer("Asha One", "asha@example.in", "9811111111"), "u-admin", "admin", "127.0.0.1");
        Assert.True(first.ok);

        // Same email, different case and a different phone -> still rejected.
        var dup = await svc.CreateAsync(
            NewCustomer("Asha Two", "ASHA@EXAMPLE.IN", "9822222222"), "u-admin", "admin", "127.0.0.1");

        Assert.False(dup.ok);
        Assert.Contains("already exists", dup.error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await tdb.Db.Customers.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicatePhone_WithDifferentFormatting()
    {
        using var tdb = await CreateDbAsync();
        var svc = Service(tdb);

        var first = await svc.CreateAsync(
            NewCustomer("Phone One", "phone1@example.in", "+91 98111 22334"), "u-admin", "admin", "127.0.0.1");
        Assert.True(first.ok);
        Assert.Equal("9811122334", first.customer!.Phone); // normalized on save

        // Same digits, plain formatting -> rejected by normalized comparison.
        var dup = await svc.CreateAsync(
            NewCustomer("Phone Two", "phone2@example.in", "9811122334"), "u-admin", "admin", "127.0.0.1");

        Assert.False(dup.ok);
        Assert.Contains("already exists", dup.error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetPagedAsync_ScopesSalesExecutiveToOwnRecords()
    {
        using var tdb = await CreateDbAsync();
        var svc = Service(tdb);

        await svc.CreateAsync(NewCustomer("Mine", "mine@example.in", "9811111111",
            assignedToId: "sales-1", createdById: "sales-1"), "sales-1", "sales", null);
        await svc.CreateAsync(NewCustomer("Theirs", "theirs@example.in", "9822222222",
            assignedToId: "mgr-1", createdById: "u-admin"), "u-admin", "admin", null);

        var salesView = await svc.GetPagedAsync(null, null, 1, 20, "sales-1", seeAll: false);
        Assert.Single(salesView);
        Assert.Equal("Mine", salesView.First().CustomerName);

        var adminView = await svc.GetPagedAsync(null, null, 1, 20, "u-admin", seeAll: true);
        Assert.Equal(2, adminView.TotalCount);

        // GetByIdAsync also hides out-of-scope records (controllers map null -> 404).
        var theirsId = (await svc.GetPagedAsync(null, null, 1, 20, "u-admin", seeAll: true))
            .First(c => c.CustomerName == "Theirs").CustomerId;
        Assert.Null(await svc.GetByIdAsync(theirsId, "sales-1", seeAll: false));
        Assert.NotNull(await svc.GetByIdAsync(theirsId, "u-admin", seeAll: true));
    }

    [Fact]
    public async Task DeactivateAsync_SoftDeletes_SetsStatusInactive()
    {
        using var tdb = await CreateDbAsync();
        var svc = Service(tdb);

        var created = await svc.CreateAsync(
            NewCustomer("Deact Me", "deact@example.in", "9811111111"), "u-admin", "admin", null);
        Assert.True(created.ok);
        var id = created.customer!.CustomerId;

        var (ok, error) = await svc.DeactivateAsync(id, "u-admin", "admin", null);
        Assert.True(ok);
        Assert.Null(error);

        var after = await tdb.Db.Customers.FindAsync(id);
        Assert.NotNull(after);
        Assert.Equal(CustomerStatus.Inactive, after.Status); // soft delete: row still exists

        // Second deactivation is rejected.
        var again = await svc.DeactivateAsync(id, "u-admin", "admin", null);
        Assert.False(again.ok);
        Assert.Contains("already inactive", again.error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_WritesAuditLogRow()
    {
        using var tdb = await CreateDbAsync();
        var svc = Service(tdb);

        var created = await svc.CreateAsync(
            NewCustomer("Audit Me", "audit@example.in", "9811111111"), "u-7", "tester", "10.0.0.1");
        Assert.True(created.ok);

        var log = await tdb.Db.AuditLogs
            .SingleOrDefaultAsync(a => a.Action == "Create Customer"
                && a.RecordId == created.customer!.CustomerId.ToString());

        Assert.NotNull(log);
        Assert.Equal("Customer", log.EntityName);
        Assert.Equal("u-7", log.UserId);
        Assert.Equal("10.0.0.1", log.IpAddress);
        Assert.Contains("Audit Me", log.NewValue ?? string.Empty);
    }
}
