using System.Text;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Controllers;

/// <summary>
/// CRM reports. Thin: all queries live in IReportService; this controller
/// only binds filters, serves HTML or CSV, and writes audit entries.
/// </summary>
[Authorize]
public class ReportsController : CrmBaseController
{
    private const int PageSize = 15;
    private const int CsvPageSize = 5000;

    private readonly IReportService _reports;
    private readonly IAuditService _audit;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportsController(
        IReportService reports,
        IAuditService audit,
        UserManager<ApplicationUser> userManager)
    {
        _reports = reports;
        _audit = audit;
        _userManager = userManager;
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    private Task AuditViewAsync(string reportName)
        => _audit.LogAsync("Viewed Report", "Report", reportName,
            userId: CurrentUserId, userName: CurrentUserName, ipAddress: ClientIp);

    private Task AuditExportAsync(string reportName)
        => _audit.LogAsync("Exported Report CSV", "Report", reportName,
            userId: CurrentUserId, userName: CurrentUserName, ipAddress: ClientIp);

    /// <summary>Users eligible as assignees: active SalesExecutives and Managers.</summary>
    private async Task<List<ApplicationUser>> GetAssignableUsersAsync()
    {
        var execs = await _userManager.GetUsersInRoleAsync(RoleNames.SalesExecutive);
        var managers = await _userManager.GetUsersInRoleAsync(RoleNames.Manager);
        return execs.Concat(managers)
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .ToList();
    }

    private static bool WantsCsv(string? format)
        => string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);

    // GET: /Reports — menu of available reports.
    public IActionResult Index() => View();

    // GET: /Reports/Customers
    public async Task<IActionResult> Customers(
        CustomerStatus? status, string? search, int page = 1,
        string? sortBy = null, string? sortDir = null, string? format = null)
    {
        var filters = new CustomerReportFilters
        {
            Status = status, Search = search,
            SortBy = string.IsNullOrWhiteSpace(sortBy) ? "created" : sortBy,
            SortDir = string.IsNullOrWhiteSpace(sortDir) ? "desc" : sortDir
        };

        if (WantsCsv(format))
        {
            var all = await _reports.GetCustomerReportAsync(
                CurrentUserId, SeesAllRecords, filters, 1, CsvPageSize);
            await AuditExportAsync("Customer Report");
            return Csv(all.Items, "customer-report",
                ("Code", r => r.CustomerCode), ("Name", r => r.CustomerName),
                ("Company", r => r.CompanyName ?? ""), ("Email", r => r.Email),
                ("Phone", r => r.Phone), ("City", r => r.City ?? ""),
                ("State", r => r.State ?? ""), ("Status", r => r.Status.ToString()),
                ("Assigned To", r => r.AssignedToName ?? ""),
                ("Created By", r => r.CreatedByName ?? ""),
                ("Created", r => r.CreatedDate.ToString("yyyy-MM-dd HH:mm")));
        }

        var model = await _reports.GetCustomerReportAsync(
            CurrentUserId, SeesAllRecords, filters, Math.Max(1, page), PageSize);
        await AuditViewAsync("Customer Report");

        ViewBag.Filters = filters;
        return View(model);
    }

    // GET: /Reports/Leads
    public async Task<IActionResult> Leads(
        LeadStatus? status, string? assignedTo, DateTime? from, DateTime? to,
        string? search, int page = 1,
        string? sortBy = null, string? sortDir = null, string? format = null)
    {
        var filters = new LeadReportFilters
        {
            Status = status, AssignedToId = assignedTo, From = from, To = to, Search = search,
            SortBy = string.IsNullOrWhiteSpace(sortBy) ? "created" : sortBy,
            SortDir = string.IsNullOrWhiteSpace(sortDir) ? "desc" : sortDir
        };

        if (WantsCsv(format))
        {
            var all = await _reports.GetLeadReportAsync(
                CurrentUserId, SeesAllRecords, filters, 1, CsvPageSize);
            await AuditExportAsync("Lead Report");
            return Csv(all.Items, "lead-report",
                ("Code", r => r.LeadCode), ("Name", r => r.LeadName),
                ("Company", r => r.CompanyName ?? ""), ("Source", r => r.Source ?? ""),
                ("Email", r => r.Email ?? ""), ("Phone", r => r.Phone ?? ""),
                ("Status", r => r.Status.ToString()), ("Priority", r => r.Priority.ToString()),
                ("Expected Value", r => r.ExpectedValue.ToString("0.00")),
                ("Assigned To", r => r.AssignedToName ?? ""),
                ("Created By", r => r.CreatedByName ?? ""),
                ("Created", r => r.CreatedDate.ToString("yyyy-MM-dd HH:mm")));
        }

        var model = await _reports.GetLeadReportAsync(
            CurrentUserId, SeesAllRecords, filters, Math.Max(1, page), PageSize);
        await AuditViewAsync("Lead Report");

        ViewBag.Filters = filters;
        ViewBag.ShowAssigneeFilter = SeesAllRecords;
        if (SeesAllRecords)
            ViewBag.AssigneeUsers = await GetAssignableUsersAsync();
        return View(model);
    }

    // GET: /Reports/FollowUps
    public async Task<IActionResult> FollowUps(
        FollowUpBucket bucket = FollowUpBucket.All, DateTime? from = null, DateTime? to = null,
        int page = 1, string? sortBy = null, string? sortDir = null, string? format = null)
    {
        var filters = new FollowUpReportFilters
        {
            Bucket = bucket, From = from, To = to,
            SortBy = string.IsNullOrWhiteSpace(sortBy) ? "date" : sortBy,
            SortDir = string.IsNullOrWhiteSpace(sortDir) ? "asc" : sortDir
        };

        if (WantsCsv(format))
        {
            var all = await _reports.GetFollowUpReportAsync(
                CurrentUserId, SeesAllRecords, filters, 1, CsvPageSize);
            await AuditExportAsync("Follow-Up Report");
            return Csv(all.Items, "follow-up-report",
                ("Type", r => r.FollowUpType.ToString()),
                ("Related To", r => r.RelatedTo), ("Related Type", r => r.RelatedType),
                ("Follow-Up Date", r => r.FollowUpDate.ToString("yyyy-MM-dd HH:mm")),
                ("Status", r => r.Status.ToString()),
                ("Overdue", r => r.IsOverdue ? "Yes" : "No"),
                ("Remarks", r => r.Remarks ?? ""),
                ("Assigned To", r => r.AssignedToName ?? ""),
                ("Created By", r => r.CreatedByName ?? ""),
                ("Created", r => r.CreatedDate.ToString("yyyy-MM-dd HH:mm")));
        }

        var model = await _reports.GetFollowUpReportAsync(
            CurrentUserId, SeesAllRecords, filters, Math.Max(1, page), PageSize);
        await AuditViewAsync("Follow-Up Report");

        ViewBag.Filters = filters;
        return View(model);
    }

    // GET: /Reports/Opportunities
    public async Task<IActionResult> Opportunities(
        OpportunityStage? stage, DateTime? from, DateTime? to, string? search, int page = 1,
        string? sortBy = null, string? sortDir = null, string? format = null)
    {
        var filters = new OpportunityReportFilters
        {
            Stage = stage, From = from, To = to, Search = search,
            SortBy = string.IsNullOrWhiteSpace(sortBy) ? "created" : sortBy,
            SortDir = string.IsNullOrWhiteSpace(sortDir) ? "desc" : sortDir
        };

        if (WantsCsv(format))
        {
            var all = await _reports.GetOpportunityReportAsync(
                CurrentUserId, SeesAllRecords, filters, 1, CsvPageSize);
            await AuditExportAsync("Opportunity Report");
            return Csv(all.Items, "opportunity-report",
                ("Name", r => r.OpportunityName), ("Customer", r => r.CustomerName ?? ""),
                ("Amount", r => r.Amount.ToString("0.00")),
                ("Stage", r => r.Stage.ToString()),
                ("Probability %", r => r.Probability.ToString()),
                ("Weighted Amount", r => r.WeightedAmount.ToString("0.00")),
                ("Expected Close", r => r.ExpectedCloseDate.ToString("yyyy-MM-dd")),
                ("Source", r => r.Source ?? ""),
                ("Assigned To", r => r.AssignedToName ?? ""),
                ("Created", r => r.CreatedDate.ToString("yyyy-MM-dd HH:mm")));
        }

        var model = await _reports.GetOpportunityReportAsync(
            CurrentUserId, SeesAllRecords, filters, Math.Max(1, page), PageSize);
        await AuditViewAsync("Opportunity Report");

        ViewBag.Filters = filters;
        return View(model);
    }

    // GET: /Reports/Pipeline
    public async Task<IActionResult> Pipeline(
        DateTime? from = null, DateTime? to = null, string? format = null)
    {
        var filters = new PipelineReportFilters { From = from, To = to };
        var model = await _reports.GetPipelineReportAsync(CurrentUserId, SeesAllRecords, filters);

        if (WantsCsv(format))
        {
            var sb = new StringBuilder();
            AppendCsvSection(sb, "Stage-wise Pipeline",
                model.Stages,
                ("Stage", r => r.Stage.ToString()), ("Count", r => r.Count.ToString()),
                ("Total Amount", r => r.TotalAmount.ToString("0.00")),
                ("Weighted Amount", r => r.WeightedAmount.ToString("0.00")));
            sb.AppendLine();
            AppendCsvSection(sb, "Owner-wise Pipeline",
                model.ByOwner,
                ("Owner", r => r.OwnerName), ("Count", r => r.Count.ToString()),
                ("Total Amount", r => r.TotalAmount.ToString("0.00")),
                ("Weighted Amount", r => r.WeightedAmount.ToString("0.00")));
            await AuditExportAsync("Pipeline Report");
            return CsvFile(sb.ToString(), "pipeline-report");
        }

        await AuditViewAsync("Pipeline Report");
        ViewBag.Filters = filters;
        return View(model);
    }

    // GET: /Reports/Conversion
    public async Task<IActionResult> Conversion(
        ConversionGroupBy groupBy = ConversionGroupBy.Month,
        DateTime? from = null, DateTime? to = null, string? format = null)
    {
        var filters = new ConversionReportFilters { GroupBy = groupBy, From = from, To = to };
        var model = await _reports.GetConversionReportAsync(CurrentUserId, SeesAllRecords, filters);

        if (WantsCsv(format))
        {
            await AuditExportAsync("Conversion Report");
            return Csv(model.Rows, "conversion-report",
                (groupBy == ConversionGroupBy.Assignee ? "Assignee" : "Month", r => r.GroupLabel),
                ("Total Leads", r => r.Total.ToString()),
                ("Converted", r => r.Converted.ToString()),
                ("Lost", r => r.Lost.ToString()),
                ("Conversion %", r => r.ConversionRate.ToString("0.0")));
        }

        await AuditViewAsync("Conversion Report");
        ViewBag.Filters = filters;
        return View(model);
    }

    // GET: /Reports/UserActivity
    public async Task<IActionResult> UserActivity(
        string? userId, DateTime? from, DateTime? to, int page = 1,
        string? sortBy = null, string? sortDir = null, string? format = null)
    {
        var filters = new UserActivityReportFilters
        {
            UserId = userId, From = from, To = to,
            SortBy = string.IsNullOrWhiteSpace(sortBy) ? "total" : sortBy,
            SortDir = string.IsNullOrWhiteSpace(sortDir) ? "desc" : sortDir
        };

        if (WantsCsv(format))
        {
            var all = await _reports.GetUserActivityReportAsync(
                CurrentUserId, SeesAllRecords, filters, 1, CsvPageSize);
            await AuditExportAsync("User Activity Report");
            return Csv(all.Items, "user-activity-report",
                ("User", r => r.FullName), ("Username", r => r.UserName),
                ("Logins", r => r.Logins.ToString()),
                ("Creates", r => r.Creates.ToString()),
                ("Updates", r => r.Updates.ToString()),
                ("Deletes", r => r.Deletes.ToString()),
                ("Exports", r => r.Exports.ToString()),
                ("Other", r => r.Other.ToString()),
                ("Total", r => r.Total.ToString()),
                ("Last Activity", r => r.LastActivity?.ToString("yyyy-MM-dd HH:mm") ?? ""));
        }

        var model = await _reports.GetUserActivityReportAsync(
            CurrentUserId, SeesAllRecords, filters, Math.Max(1, page), PageSize);
        await AuditViewAsync("User Activity Report");

        ViewBag.Filters = filters;
        ViewBag.ShowUserFilter = SeesAllRecords;
        if (SeesAllRecords)
            ViewBag.FilterUsers = await _userManager.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();
        return View(model);
    }

    // GET: /Reports/AuditLogs — Admin only.
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> AuditLogs(
        string? entityName, string? action, string? userId,
        DateTime? from, DateTime? to, int page = 1, string? format = null)
    {
        var filters = new AuditLogFilters
        {
            EntityName = entityName, Action = action, UserId = userId, From = from, To = to
        };

        if (WantsCsv(format))
        {
            var all = await _reports.GetAuditLogAsync(filters, 1, CsvPageSize);
            await AuditExportAsync("Audit Log Report");
            return Csv(all.Items, "audit-log",
                ("Id", r => r.AuditLogId.ToString()),
                ("Timestamp", r => r.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss")),
                ("User", r => r.UserName ?? ""), ("Action", r => r.Action),
                ("Entity", r => r.EntityName ?? ""), ("Record Id", r => r.RecordId ?? ""),
                ("IP", r => r.IpAddress ?? ""));
        }

        var model = await _reports.GetAuditLogAsync(filters, Math.Max(1, page), PageSize);
        // Viewing the audit log is itself audited (append-only; no delete exists).
        await AuditViewAsync("Audit Log Report");

        var (entities, actions) = await _reports.GetAuditLookupsAsync();
        ViewBag.Filters = filters;
        ViewBag.EntityNames = entities;
        ViewBag.Actions = actions;
        ViewBag.FilterUsers = await _userManager.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync();
        return View(model);
    }

    // ---------------------------------------------------------- CSV helpers

    private FileContentResult CsvFile(string csv, string name)
    {
        var bytes = Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv",
            $"{name}-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }

    private FileContentResult Csv<T>(
        IEnumerable<T> rows, string name,
        params (string Header, Func<T, string> Value)[] columns)
    {
        var sb = new StringBuilder();
        AppendCsvSection(sb, null, rows, columns);
        return CsvFile(sb.ToString(), name);
    }

    private static void AppendCsvSection<T>(
        StringBuilder sb, string? title, IEnumerable<T> rows,
        params (string Header, Func<T, string> Value)[] columns)
    {
        if (title is not null)
            sb.AppendLine(title);
        sb.AppendLine(string.Join(",", columns.Select(c => Escape(c.Header))));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", columns.Select(c => Escape(c.Value(row)))));
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
