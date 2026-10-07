using AcxiomCRM.Web.Helpers;
using AcxiomCRM.Web.Models;

namespace AcxiomCRM.Web.Services;

public interface IActivityService
{
    Task<PaginatedList<Activity>> GetPagedAsync(
        string? search, ActivityType? type, ActivityStatus? status, int page, int pageSize,
        string? userId, bool seeAll, CancellationToken ct = default);

    Task<Activity?> GetByIdAsync(int id, string? userId, bool seeAll, CancellationToken ct = default);

    Task<Activity> CreateAsync(
        Activity activity, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default);

    Task<Activity> UpdateAsync(
        Activity activity, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default);

    Task DeleteAsync(
        int id, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default);
}
