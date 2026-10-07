using AcxiomCRM.Web.Models;

namespace AcxiomCRM.Web.Services;

/// <summary>Generic server-side page wrapper returned by the report service.</summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Create(IEnumerable<T> items, int totalCount, int page, int pageSize)
        => new() { Items = items.ToList(), TotalCount = totalCount, Page = page, PageSize = pageSize };
}

// ------------------------------------------------------------------ filters

public sealed class CustomerReportFilters
{
    public CustomerStatus? Status { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; } = "created";
    public string? SortDir { get; set; } = "desc";
}

public sealed class LeadReportFilters
{
    public LeadStatus? Status { get; set; }
    public string? AssignedToId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; } = "created";
    public string? SortDir { get; set; } = "desc";
}

/// <summary>Follow-up time bucket: specific statuses plus upcoming/overdue derived buckets.</summary>
public enum FollowUpBucket
{
    All = 0,
    Planned = 1,
    Upcoming = 2,
    Overdue = 3,
    Completed = 4,
    Missed = 5,
    Cancelled = 6
}

public sealed class FollowUpReportFilters
{
    public FollowUpBucket Bucket { get; set; } = FollowUpBucket.All;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? SortBy { get; set; } = "date";
    public string? SortDir { get; set; } = "asc";
}

public sealed class OpportunityReportFilters
{
    public OpportunityStage? Stage { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; } = "created";
    public string? SortDir { get; set; } = "desc";
}

public sealed class PipelineReportFilters
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public enum ConversionGroupBy
{
    Month = 0,
    Assignee = 1
}

public sealed class ConversionReportFilters
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public ConversionGroupBy GroupBy { get; set; } = ConversionGroupBy.Month;
}

public sealed class UserActivityReportFilters
{
    public string? UserId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? SortBy { get; set; } = "total";
    public string? SortDir { get; set; } = "desc";
}

public sealed class AuditLogFilters
{
    public string? EntityName { get; set; }
    public string? Action { get; set; }
    public string? UserId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

// ------------------------------------------------------------------ rows

public sealed class CustomerReportRow
{
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string? City { get; init; }
    public string? State { get; init; }
    public CustomerStatus Status { get; init; }
    public string? AssignedToName { get; init; }
    public string? CreatedByName { get; init; }
    public DateTime CreatedDate { get; init; }
}

public sealed class LeadReportRow
{
    public string LeadCode { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public string? Source { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public LeadStatus Status { get; init; }
    public LeadPriority Priority { get; init; }
    public decimal ExpectedValue { get; init; }
    public string? AssignedToName { get; init; }
    public string? CreatedByName { get; init; }
    public DateTime CreatedDate { get; init; }
}

public sealed class FollowUpReportRow
{
    public int FollowUpId { get; init; }
    public FollowUpType FollowUpType { get; init; }
    public string RelatedTo { get; init; } = string.Empty;
    public string RelatedType { get; init; } = string.Empty;
    public DateTime FollowUpDate { get; init; }
    public FollowUpStatus Status { get; init; }
    public bool IsOverdue { get; init; }
    public string? Remarks { get; init; }
    public string? AssignedToName { get; init; }
    public string? CreatedByName { get; init; }
    public DateTime CreatedDate { get; init; }
}

public sealed class OpportunityReportRow
{
    public string OpportunityName { get; init; } = string.Empty;
    public string? CustomerName { get; init; }
    public decimal Amount { get; init; }
    public OpportunityStage Stage { get; init; }
    public int Probability { get; init; }
    public decimal WeightedAmount { get; init; }
    public DateTime ExpectedCloseDate { get; init; }
    public string? Source { get; init; }
    public string? AssignedToName { get; init; }
    public DateTime CreatedDate { get; init; }
}

public sealed class PipelineStageRow
{
    public OpportunityStage Stage { get; init; }
    public int Count { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal WeightedAmount { get; init; }
}

public sealed class PipelineOwnerRow
{
    public string OwnerName { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal WeightedAmount { get; init; }
}

public sealed class PipelineReportResult
{
    public IReadOnlyList<PipelineStageRow> Stages { get; init; } = Array.Empty<PipelineStageRow>();
    public IReadOnlyList<PipelineOwnerRow> ByOwner { get; init; } = Array.Empty<PipelineOwnerRow>();
    public int TotalCount { get; init; }
    public decimal GrandTotalAmount { get; init; }
    public decimal GrandTotalWeighted { get; init; }
}

public sealed class ConversionReportRow
{
    public string GroupLabel { get; init; } = string.Empty;
    public int Total { get; init; }
    public int Converted { get; init; }
    public int Lost { get; init; }
    public decimal ConversionRate => Total == 0 ? 0m : Math.Round(Converted * 100m / Total, 1);
}

public sealed class ConversionReportResult
{
    public IReadOnlyList<ConversionReportRow> Rows { get; init; } = Array.Empty<ConversionReportRow>();
    public int Total { get; init; }
    public int Converted { get; init; }
    public decimal OverallRate => Total == 0 ? 0m : Math.Round(Converted * 100m / Total, 1);
}

public sealed class UserActivityReportRow
{
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public int Logins { get; init; }
    public int Creates { get; init; }
    public int Updates { get; init; }
    public int Deletes { get; init; }
    public int Exports { get; init; }
    public int Other { get; init; }
    public int Total => Logins + Creates + Updates + Deletes + Exports + Other;
    public DateTime? LastActivity { get; init; }
}

public sealed class AuditLogRow
{
    public long AuditLogId { get; init; }
    public DateTime CreatedDate { get; init; }
    public string? UserName { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? EntityName { get; init; }
    public string? RecordId { get; init; }
    public string? IpAddress { get; init; }
}
