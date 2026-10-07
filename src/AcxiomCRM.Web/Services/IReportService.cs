namespace AcxiomCRM.Web.Services;

/// <summary>
/// Read-only reporting queries over the CRM. Every method applies the standard
/// visibility rule: when <c>seeAll</c> is false (SalesExecutive), only records
/// where AssignedToId == userId OR CreatedById == userId are included. All rows
/// are plain DTOs, never EF entities.
/// </summary>
public interface IReportService
{
    Task<PagedResult<CustomerReportRow>> GetCustomerReportAsync(
        string? userId, bool seeAll, CustomerReportFilters filters,
        int page, int pageSize, CancellationToken ct = default);

    Task<PagedResult<LeadReportRow>> GetLeadReportAsync(
        string? userId, bool seeAll, LeadReportFilters filters,
        int page, int pageSize, CancellationToken ct = default);

    Task<PagedResult<FollowUpReportRow>> GetFollowUpReportAsync(
        string? userId, bool seeAll, FollowUpReportFilters filters,
        int page, int pageSize, CancellationToken ct = default);

    Task<PagedResult<OpportunityReportRow>> GetOpportunityReportAsync(
        string? userId, bool seeAll, OpportunityReportFilters filters,
        int page, int pageSize, CancellationToken ct = default);

    Task<PipelineReportResult> GetPipelineReportAsync(
        string? userId, bool seeAll, PipelineReportFilters filters,
        CancellationToken ct = default);

    Task<ConversionReportResult> GetConversionReportAsync(
        string? userId, bool seeAll, ConversionReportFilters filters,
        CancellationToken ct = default);

    Task<PagedResult<UserActivityReportRow>> GetUserActivityReportAsync(
        string? userId, bool seeAll, UserActivityReportFilters filters,
        int page, int pageSize, CancellationToken ct = default);

    /// <summary>Audit log viewer. Callers must be Admin (enforced by the controller).</summary>
    Task<PagedResult<AuditLogRow>> GetAuditLogAsync(
        AuditLogFilters filters, int page, int pageSize,
        CancellationToken ct = default);

    /// <summary>Distinct entity/action/user names to populate audit filter dropdowns.</summary>
    Task<(IReadOnlyList<string> EntityNames, IReadOnlyList<string> Actions)> GetAuditLookupsAsync(
        CancellationToken ct = default);
}
