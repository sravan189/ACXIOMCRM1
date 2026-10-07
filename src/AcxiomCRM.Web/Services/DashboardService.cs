using System.Globalization;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.ViewModels.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Services;

/// <summary>
/// Dashboard aggregation over Customers, Leads and Opportunities.
///
/// Scope rule: when <c>seeAll</c> is false, every entity set is restricted to
/// records where AssignedToId == userId OR CreatedById == userId.
/// Date rule: the from/to filter applies to CreatedDate (inclusive); the
/// Monthly Sales chart keeps its fixed rolling last-6-months window and is
/// keyed by ExpectedCloseDate instead.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _db;

    // Lead statuses that count as closed (not "open").
    private static readonly LeadStatus[] ClosedLeadStatuses =
        { LeadStatus.Unqualified, LeadStatus.Converted, LeadStatus.Lost };

    public DashboardService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(string userId, bool seeAll, DateTime? from, DateTime? to)
    {
        var customers = Filter(_db.Customers, c => c.CreatedDate, from, to, c => c.AssignedToId == userId || c.CreatedById == userId, seeAll);
        var leads = Filter(_db.Leads, l => l.CreatedDate, from, to, l => l.AssignedToId == userId || l.CreatedById == userId, seeAll);
        var opps = Filter(_db.Opportunities, o => o.CreatedDate, from, to, o => o.AssignedToId == userId || o.CreatedById == userId, seeAll);

        var openLeads = leads.Where(l => !ClosedLeadStatuses.Contains(l.Status));
        var openOpps = opps.Where(o => o.Stage != OpportunityStage.Won && o.Stage != OpportunityStage.Lost);

        var vm = new DashboardViewModel
        {
            From = from,
            To = to,

            TotalCustomers = await customers.CountAsync(),
            TotalLeads = await leads.CountAsync(),
            OpenLeads = await openLeads.CountAsync(),
            TotalOpportunities = await opps.CountAsync(),
            OpenOpportunities = await openOpps.CountAsync(),
            WonOpportunities = await opps.CountAsync(o => o.Stage == OpportunityStage.Won),
            LostOpportunities = await opps.CountAsync(o => o.Stage == OpportunityStage.Lost),
            // Note: sums are computed client-side. EF Core's SQLite provider cannot
            // translate Sum() over decimal columns (works on SQL Server, fails on
            // SQLite), and the pipeline result sets are small enough to aggregate
            // in memory on any provider.
            TotalPipelineValue = (await openOpps.Select(o => o.Amount).ToListAsync()).Sum(),
        };

        // Lead status distribution (all 6 statuses, zero-filled).
        var leadStatusCounts = Enum.GetNames<LeadStatus>().ToDictionary(s => s, _ => 0);
        var leadGroups = await leads
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        foreach (var g in leadGroups)
            leadStatusCounts[g.Status.ToString()] = g.Count;
        vm.LeadStatusCounts = leadStatusCounts;

        // Opportunity pipeline amounts per stage (all 5 stages, zero-filled).
        var stageAmounts = Enum.GetNames<OpportunityStage>().ToDictionary(s => s, _ => 0m);
        var stageRows = await opps
            .Select(o => new { o.Stage, o.Amount })
            .ToListAsync();
        foreach (var g in stageRows.GroupBy(r => r.Stage))
            stageAmounts[g.Key.ToString()] = g.Sum(r => r.Amount);
        vm.OpportunityStageAmounts = stageAmounts;

        // Monthly sales: won opportunities grouped by ExpectedCloseDate month, last 6 months.
        vm.MonthlySales = await GetMonthlySalesAsync(userId, seeAll);

        return vm;
    }

    private async Task<List<MonthlySalesPoint>> GetMonthlySalesAsync(string userId, bool seeAll)
    {
        var utcNow = DateTime.UtcNow;
        var windowStart = new DateTime(utcNow.Year, utcNow.Month, 1).AddMonths(-5);

        var wonOpps = Filter(
            _db.Opportunities.Where(o => o.Stage == OpportunityStage.Won && o.ExpectedCloseDate >= windowStart),
            o => o.CreatedDate, null, null,
            o => o.AssignedToId == userId || o.CreatedById == userId,
            seeAll);

        // Client-side grouping: SQLite cannot translate SUM() over decimals.
        var rows = await wonOpps
            .Select(o => new { o.ExpectedCloseDate.Year, o.ExpectedCloseDate.Month, o.Amount })
            .ToListAsync();

        var totals = rows
            .GroupBy(r => (r.Year, r.Month))
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));

        return Enumerable.Range(0, 6)
            .Select(i => windowStart.AddMonths(i))
            .Select(d => new MonthlySalesPoint
            {
                Month = d.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                Total = totals.TryGetValue((d.Year, d.Month), out var t) ? t : 0m,
            })
            .ToList();
    }

    /// <summary>
    /// Applies the record scope (all records, or only assigned/created by the user)
    /// plus the optional CreatedDate range. Stays an IQueryable so EF Core translates it.
    /// </summary>
    private static IQueryable<T> Filter<T>(
        IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, DateTime>> createdDate,
        DateTime? from, DateTime? to,
        System.Linq.Expressions.Expression<Func<T, bool>> scope,
        bool seeAll) where T : class
    {
        if (!seeAll)
            query = query.Where(scope);
        if (from.HasValue)
            query = query.Where(createdDate.ComposeWith(d => d >= from.Value.Date));
        if (to.HasValue)
            query = query.Where(createdDate.ComposeWith(d => d < to.Value.Date.AddDays(1))); // inclusive end of day
        return query;
    }
}

/// <summary>
/// Rebinds the parameter of <paramref name="outer"/> so the composed
/// predicate keeps a single parameter and stays translatable by EF Core.
/// </summary>
internal static class ExpressionExtensions
{
    public static System.Linq.Expressions.Expression<Func<T, bool>> ComposeWith<T>(
        this System.Linq.Expressions.Expression<Func<T, DateTime>> outer,
        System.Linq.Expressions.Expression<Func<DateTime, bool>> inner)
    {
        var replaced = new ParameterReplacer(inner.Parameters[0], outer.Body).Visit(inner.Body);
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(replaced!, outer.Parameters);
    }

    private sealed class ParameterReplacer(
        System.Linq.Expressions.ParameterExpression from,
        System.Linq.Expressions.Expression to) : System.Linq.Expressions.ExpressionVisitor
    {
        protected override System.Linq.Expressions.Expression VisitParameter(
            System.Linq.Expressions.ParameterExpression node)
            => node == from ? to : node;
    }
}
