using System.Globalization;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Services;

/// <summary>
/// Read-only report queries. Visibility scoping matches the rest of the app:
/// SalesExecutives see only records they are assigned to or created.
///
/// Provider note (mirrors DashboardService / OpportunityService): the app runs
/// on SQL Server in production but on SQLite locally, and EF Core's SQLite
/// provider cannot translate Sum() over decimal columns or ORDER BY decimal.
/// So every report materializes its (small, filter-scoped) result set with
/// ToListAsync and then sorts/pages/aggregates in memory. GroupBy+Count on
/// ints and Max(DateTime) are safe server-side.
/// </summary>
public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;

    public ReportService(ApplicationDbContext db)
    {
        _db = db;
    }

    // ----------------------------------------------------------- customers

    public async Task<PagedResult<CustomerReportRow>> GetCustomerReportAsync(
        string? userId, bool seeAll, CustomerReportFilters filters,
        int page, int pageSize, CancellationToken ct = default)
    {
        var f = filters ?? new CustomerReportFilters();
        var query = Scoped(
            _db.Customers,
            c => c.AssignedToId == userId || c.CreatedById == userId,
            seeAll);

        if (f.Status.HasValue)
            query = query.Where(c => c.Status == f.Status.Value);

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            query = query.Where(c =>
                c.CustomerName.Contains(s) ||
                c.CustomerCode.Contains(s) ||
                (c.CompanyName ?? "").Contains(s) ||
                c.Email.Contains(s) ||
                c.Phone.Contains(s));
        }

        var rows = await query
            .Select(c => new CustomerReportRow
            {
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                CompanyName = c.CompanyName,
                Email = c.Email,
                Phone = c.Phone,
                City = c.City,
                State = c.State,
                Status = c.Status,
                AssignedToName = c.AssignedTo != null ? c.AssignedTo.FullName : null,
                CreatedByName = c.CreatedBy != null ? c.CreatedBy.FullName : null,
                CreatedDate = c.CreatedDate
            })
            .ToListAsync(ct);

        rows = Sort(rows, f.SortBy, f.SortDir,
            ("code", r => (object)r.CustomerCode),
            ("name", r => (object)r.CustomerName),
            ("company", r => (object)(r.CompanyName ?? string.Empty)),
            ("status", r => (object)r.Status),
            ("created", r => (object)r.CreatedDate))
            .ToList();

        return Page(rows, page, pageSize);
    }

    // --------------------------------------------------------------- leads

    public async Task<PagedResult<LeadReportRow>> GetLeadReportAsync(
        string? userId, bool seeAll, LeadReportFilters filters,
        int page, int pageSize, CancellationToken ct = default)
    {
        var f = filters ?? new LeadReportFilters();
        var query = Scoped(
            _db.Leads,
            l => l.AssignedToId == userId || l.CreatedById == userId,
            seeAll);

        query = ApplyCreatedRange(query, l => l.CreatedDate, f.From, f.To);

        if (f.Status.HasValue)
            query = query.Where(l => l.Status == f.Status.Value);

        if (!string.IsNullOrWhiteSpace(f.AssignedToId))
            query = query.Where(l => l.AssignedToId == f.AssignedToId);

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            query = query.Where(l =>
                l.LeadName.Contains(s) ||
                l.LeadCode.Contains(s) ||
                (l.CompanyName ?? "").Contains(s) ||
                (l.Email ?? "").Contains(s) ||
                (l.Phone ?? "").Contains(s));
        }

        var rows = await query
            .Select(l => new LeadReportRow
            {
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                CompanyName = l.CompanyName,
                Source = l.Source,
                Email = l.Email,
                Phone = l.Phone,
                Status = l.Status,
                Priority = l.Priority,
                ExpectedValue = l.ExpectedValue,
                AssignedToName = l.AssignedTo != null ? l.AssignedTo.FullName : null,
                CreatedByName = l.CreatedBy != null ? l.CreatedBy.FullName : null,
                CreatedDate = l.CreatedDate
            })
            .ToListAsync(ct);

        rows = Sort(rows, f.SortBy, f.SortDir,
            ("code", r => (object)r.LeadCode),
            ("name", r => (object)r.LeadName),
            ("company", r => (object)(r.CompanyName ?? string.Empty)),
            ("status", r => (object)r.Status),
            ("value", r => (object)r.ExpectedValue),
            ("created", r => (object)r.CreatedDate))
            .ToList();

        return Page(rows, page, pageSize);
    }

    // ----------------------------------------------------------- follow-ups

    public async Task<PagedResult<FollowUpReportRow>> GetFollowUpReportAsync(
        string? userId, bool seeAll, FollowUpReportFilters filters,
        int page, int pageSize, CancellationToken ct = default)
    {
        var f = filters ?? new FollowUpReportFilters();
        var query = Scoped(
            _db.FollowUps,
            fu => fu.AssignedToId == userId || fu.CreatedById == userId,
            seeAll);

        query = ApplyCreatedRange(query, fu => fu.CreatedDate, f.From, f.To);

        var today = DateTime.Today;
        switch (f.Bucket)
        {
            case FollowUpBucket.Planned:
                query = query.Where(fu => fu.Status == FollowUpStatus.Planned);
                break;
            case FollowUpBucket.Upcoming:
                query = query.Where(fu =>
                    fu.Status == FollowUpStatus.Planned && fu.FollowUpDate.Date >= today);
                break;
            case FollowUpBucket.Overdue:
                query = query.Where(fu =>
                    fu.Status == FollowUpStatus.Planned && fu.FollowUpDate.Date < today);
                break;
            case FollowUpBucket.Completed:
                query = query.Where(fu => fu.Status == FollowUpStatus.Completed);
                break;
            case FollowUpBucket.Missed:
                query = query.Where(fu => fu.Status == FollowUpStatus.Missed);
                break;
            case FollowUpBucket.Cancelled:
                query = query.Where(fu => fu.Status == FollowUpStatus.Cancelled);
                break;
        }

        var rows = await query
            .Select(fu => new FollowUpReportRow
            {
                FollowUpId = fu.FollowUpId,
                FollowUpType = fu.FollowUpType,
                RelatedTo = fu.Lead != null ? fu.Lead.LeadName
                    : fu.Customer != null ? fu.Customer.CustomerName : "(none)",
                RelatedType = fu.Lead != null ? "Lead" : fu.Customer != null ? "Customer" : "-",
                FollowUpDate = fu.FollowUpDate,
                Status = fu.Status,
                IsOverdue = fu.Status == FollowUpStatus.Planned && fu.FollowUpDate.Date < DateTime.Today,
                Remarks = fu.Remarks,
                AssignedToName = fu.AssignedTo != null ? fu.AssignedTo.FullName : null,
                CreatedByName = fu.CreatedBy != null ? fu.CreatedBy.FullName : null,
                CreatedDate = fu.CreatedDate
            })
            .ToListAsync(ct);

        rows = Sort(rows, f.SortBy, f.SortDir,
            ("date", r => (object)r.FollowUpDate),
            ("type", r => (object)r.FollowUpType),
            ("status", r => (object)r.Status),
            ("related", r => (object)r.RelatedTo),
            ("created", r => (object)r.CreatedDate))
            .ToList();

        return Page(rows, page, pageSize);
    }

    // -------------------------------------------------------- opportunities

    public async Task<PagedResult<OpportunityReportRow>> GetOpportunityReportAsync(
        string? userId, bool seeAll, OpportunityReportFilters filters,
        int page, int pageSize, CancellationToken ct = default)
    {
        var f = filters ?? new OpportunityReportFilters();
        var query = Scoped(
            _db.Opportunities,
            o => o.AssignedToId == userId || o.CreatedById == userId,
            seeAll);

        query = ApplyCreatedRange(query, o => o.CreatedDate, f.From, f.To);

        if (f.Stage.HasValue)
            query = query.Where(o => o.Stage == f.Stage.Value);

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            query = query.Where(o =>
                o.OpportunityName.Contains(s) ||
                (o.Customer != null && o.Customer.CustomerName.Contains(s)) ||
                (o.Source ?? "").Contains(s));
        }

        var rows = await query
            .Select(o => new OpportunityReportRow
            {
                OpportunityName = o.OpportunityName,
                CustomerName = o.Customer != null ? o.Customer.CustomerName : null,
                Amount = o.Amount,
                Stage = o.Stage,
                Probability = o.Probability,
                WeightedAmount = o.Amount * o.Probability / 100m,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Source = o.Source,
                AssignedToName = o.AssignedTo != null ? o.AssignedTo.FullName : null,
                CreatedDate = o.CreatedDate
            })
            .ToListAsync(ct);

        rows = Sort(rows, f.SortBy, f.SortDir,
            ("name", r => (object)r.OpportunityName),
            ("amount", r => (object)r.Amount),
            ("stage", r => (object)r.Stage),
            ("probability", r => (object)r.Probability),
            ("weighted", r => (object)r.WeightedAmount),
            ("close", r => (object)r.ExpectedCloseDate),
            ("created", r => (object)r.CreatedDate))
            .ToList();

        return Page(rows, page, pageSize);
    }

    // ------------------------------------------------------------- pipeline

    public async Task<PipelineReportResult> GetPipelineReportAsync(
        string? userId, bool seeAll, PipelineReportFilters filters,
        CancellationToken ct = default)
    {
        var f = filters ?? new PipelineReportFilters();
        var query = Scoped(
            _db.Opportunities,
            o => o.AssignedToId == userId || o.CreatedById == userId,
            seeAll);

        query = ApplyCreatedRange(query, o => o.CreatedDate, f.From, f.To);

        // Materialize first: SQLite cannot SUM / ORDER BY decimals.
        var rows = await query
            .Select(o => new
            {
                o.Stage,
                o.Amount,
                Weighted = o.Amount * o.Probability / 100m,
                OwnerName = o.AssignedTo != null ? o.AssignedTo.FullName : "(unassigned)"
            })
            .ToListAsync(ct);

        var stages = Enum.GetValues<OpportunityStage>()
            .Select(stage =>
            {
                var inStage = rows.Where(r => r.Stage == stage).ToList();
                return new PipelineStageRow
                {
                    Stage = stage,
                    Count = inStage.Count,
                    TotalAmount = inStage.Sum(r => r.Amount),
                    WeightedAmount = inStage.Sum(r => r.Weighted)
                };
            })
            .ToList();

        var byOwner = rows
            .GroupBy(r => r.OwnerName)
            .Select(g => new PipelineOwnerRow
            {
                OwnerName = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(r => r.Amount),
                WeightedAmount = g.Sum(r => r.Weighted)
            })
            .OrderByDescending(o => o.TotalAmount)
            .ToList();

        return new PipelineReportResult
        {
            Stages = stages,
            ByOwner = byOwner,
            TotalCount = rows.Count,
            GrandTotalAmount = rows.Sum(r => r.Amount),
            GrandTotalWeighted = rows.Sum(r => r.Weighted)
        };
    }

    // ------------------------------------------------------------ conversion

    public async Task<ConversionReportResult> GetConversionReportAsync(
        string? userId, bool seeAll, ConversionReportFilters filters,
        CancellationToken ct = default)
    {
        var f = filters ?? new ConversionReportFilters();
        var query = Scoped(
            _db.Leads,
            l => l.AssignedToId == userId || l.CreatedById == userId,
            seeAll);

        query = ApplyCreatedRange(query, l => l.CreatedDate, f.From, f.To);

        var rows = await query
            .Select(l => new ConversionSourceRow
            {
                Status = l.Status,
                Year = l.CreatedDate.Year,
                Month = l.CreatedDate.Month,
                Assignee = l.AssignedTo != null ? l.AssignedTo.FullName : "(unassigned)"
            })
            .ToListAsync(ct);

        List<ConversionReportRow> groups;
        if (f.GroupBy == ConversionGroupBy.Assignee)
        {
            groups = rows
                .GroupBy(r => r.Assignee)
                .OrderBy(g => g.Key)
                .Select(g => ToConversionRow(g.Key, g.ToList()))
                .ToList();
        }
        else
        {
            groups = rows
                .GroupBy(r => new { r.Year, r.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => ToConversionRow(
                    new DateTime(g.Key.Year, g.Key.Month, 1)
                        .ToString("MMM yyyy", CultureInfo.InvariantCulture),
                    g.ToList()))
                .ToList();
        }

        return new ConversionReportResult
        {
            Rows = groups,
            Total = rows.Count,
            Converted = rows.Count(r => r.Status == LeadStatus.Converted)
        };
    }

    private sealed class ConversionSourceRow
    {
        public LeadStatus Status { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public string Assignee { get; set; } = string.Empty;
    }

    private static ConversionReportRow ToConversionRow(
        string label, List<ConversionSourceRow> rows)
        => new()
        {
            GroupLabel = label,
            Total = rows.Count,
            Converted = rows.Count(r => r.Status == LeadStatus.Converted),
            Lost = rows.Count(r => r.Status == LeadStatus.Lost)
        };

    // -------------------------------------------------------- user activity

    public async Task<PagedResult<UserActivityReportRow>> GetUserActivityReportAsync(
        string? userId, bool seeAll, UserActivityReportFilters filters,
        int page, int pageSize, CancellationToken ct = default)
    {
        var f = filters ?? new UserActivityReportFilters();

        var logs = _db.AuditLogs.AsQueryable();
        if (f.From.HasValue)
            logs = logs.Where(a => a.CreatedDate >= f.From.Value.Date);
        if (f.To.HasValue)
            logs = logs.Where(a => a.CreatedDate < f.To.Value.Date.AddDays(1));

        if (!string.IsNullOrWhiteSpace(f.UserId))
            logs = logs.Where(a => a.UserId == f.UserId);
        else if (!seeAll)
            // Scoped sales view: a SalesExecutive only sees their own activity.
            logs = logs.Where(a => a.UserId == userId);

        // Server-side grouping is fine here (counts + Max(DateTime) translate on SQLite).
        var grouped = await logs
            .GroupBy(a => new { a.UserId, a.UserName })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.UserName,
                Logins = g.Count(a => a.Action.StartsWith("Login")),
                Creates = g.Count(a => a.Action.StartsWith("Create")),
                Updates = g.Count(a => a.Action.StartsWith("Update")),
                Deletes = g.Count(a => a.Action.StartsWith("Delete")),
                Exports = g.Count(a => a.Action.StartsWith("Export")),
                Other = g.Count(a =>
                    !a.Action.StartsWith("Login") &&
                    !a.Action.StartsWith("Create") &&
                    !a.Action.StartsWith("Update") &&
                    !a.Action.StartsWith("Delete") &&
                    !a.Action.StartsWith("Export")),
                LastActivity = g.Max(a => a.CreatedDate)
            })
            .ToListAsync(ct);

        var names = await _db.Users
            .Select(u => new { u.Id, u.FullName, u.UserName })
            .ToListAsync(ct);
        var nameById = names.ToDictionary(u => u.Id, u => u);

        var rows = grouped
            .Select(g =>
            {
                var full = g.UserId != null && nameById.TryGetValue(g.UserId, out var u)
                    ? u.FullName : g.UserName ?? "(unknown)";
                return new UserActivityReportRow
                {
                    UserId = g.UserId ?? string.Empty,
                    UserName = g.UserName ?? string.Empty,
                    FullName = full,
                    Logins = g.Logins,
                    Creates = g.Creates,
                    Updates = g.Updates,
                    Deletes = g.Deletes,
                    Exports = g.Exports,
                    Other = g.Other,
                    LastActivity = g.LastActivity
                };
            })
            .ToList();

        rows = Sort(rows, f.SortBy, f.SortDir,
            ("user", r => (object)r.FullName),
            ("logins", r => (object)r.Logins),
            ("creates", r => (object)r.Creates),
            ("updates", r => (object)r.Updates),
            ("deletes", r => (object)r.Deletes),
            ("exports", r => (object)r.Exports),
            ("total", r => (object)r.Total),
            ("last", r => (object)(r.LastActivity ?? DateTime.MinValue)))
            .ToList();

        return Page(rows, page, pageSize);
    }

    // ------------------------------------------------------------- audit log

    public async Task<PagedResult<AuditLogRow>> GetAuditLogAsync(
        AuditLogFilters filters, int page, int pageSize,
        CancellationToken ct = default)
    {
        var f = filters ?? new AuditLogFilters();
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(f.EntityName))
            query = query.Where(a => a.EntityName == f.EntityName);
        if (!string.IsNullOrWhiteSpace(f.Action))
            query = query.Where(a => a.Action == f.Action);
        if (!string.IsNullOrWhiteSpace(f.UserId))
            query = query.Where(a => a.UserId == f.UserId);
        if (f.From.HasValue)
            query = query.Where(a => a.CreatedDate >= f.From.Value.Date);
        if (f.To.HasValue)
            query = query.Where(a => a.CreatedDate < f.To.Value.Date.AddDays(1));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedDate)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogRow
            {
                AuditLogId = a.AuditLogId,
                CreatedDate = a.CreatedDate,
                UserName = a.UserName,
                Action = a.Action,
                EntityName = a.EntityName,
                RecordId = a.RecordId,
                IpAddress = a.IpAddress
            })
            .ToListAsync(ct);

        return PagedResult<AuditLogRow>.Create(items, total, Math.Max(1, page), pageSize);
    }

    public async Task<(IReadOnlyList<string> EntityNames, IReadOnlyList<string> Actions)> GetAuditLookupsAsync(
        CancellationToken ct = default)
    {
        var entities = await _db.AuditLogs
            .Where(a => a.EntityName != null)
            .Select(a => a.EntityName!)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync(ct);
        var actions = await _db.AuditLogs
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(ct);
        return (entities, actions);
    }

    // ---------------------------------------------------------------- helpers

    private static IQueryable<T> Scoped<T>(
        IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, bool>> scope,
        bool seeAll)
        => seeAll ? query : query.Where(scope);

    private static IQueryable<T> ApplyCreatedRange<T>(
        IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, DateTime>> createdDate,
        DateTime? from, DateTime? to)
    {
        if (from.HasValue)
            query = query.Where(createdDate.ComposeWith(d => d >= from.Value.Date));
        if (to.HasValue)
            query = query.Where(createdDate.ComposeWith(d => d < to.Value.Date.AddDays(1)));
        return query;
    }

    /// <summary>In-memory ordering over an already-materialized list (SQLite-safe).</summary>
    private static IEnumerable<T> Sort<T>(
        IEnumerable<T> rows, string? sortBy, string? sortDir,
        params (string Key, Func<T, object> Selector)[] columns)
    {
        var key = string.IsNullOrWhiteSpace(sortBy) ? columns[^1].Key : sortBy.Trim().ToLowerInvariant();
        var selector = columns.FirstOrDefault(c => c.Key == key).Selector ?? columns[^1].Selector;
        return string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase)
            ? rows.OrderBy(selector)
            : rows.OrderByDescending(selector);
    }

    private static PagedResult<T> Page<T>(List<T> rows, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 5000);
        var items = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return PagedResult<T>.Create(items, rows.Count, page, pageSize);
    }
}
