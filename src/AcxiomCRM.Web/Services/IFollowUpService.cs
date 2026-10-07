using AcxiomCRM.Web.Helpers;
using AcxiomCRM.Web.Models;

namespace AcxiomCRM.Web.Services;

public interface IFollowUpService
{
    /// <summary>
    /// filter: "upcoming" (default) | "overdue" | "completed".
    /// Overdue is computed (Planned && FollowUpDate &lt; today) — display only.
    /// </summary>
    Task<PaginatedList<FollowUp>> GetPagedAsync(
        string? filter, string? search, string? assignedTo, int page, int pageSize,
        string? userId, bool seeAll, CancellationToken ct = default);

    Task<FollowUp?> GetByIdAsync(int id, string? userId, bool seeAll, CancellationToken ct = default);

    Task<FollowUp> CreateAsync(
        FollowUp followUp, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default);

    Task<FollowUp> UpdateAsync(
        FollowUp followUp, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default);

    Task DeleteAsync(
        int id, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default);

    Task<FollowUp> CompleteAsync(
        int id, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default);

    Task<FollowUp> RescheduleAsync(
        int id, DateTime newDate, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default);

    Task<FollowUp> CancelAsync(
        int id, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default);
}
