using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Helpers;
using AcxiomCRM.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Services;

public class ActivityService : IActivityService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public ActivityService(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private IQueryable<Activity> Scoped(string? userId, bool seeAll)
    {
        var q = seeAll
            ? _db.Activities.AsQueryable()
            : _db.Activities.Where(a => a.AssignedToId == userId || a.CreatedById == userId);
        return q.Include(a => a.Customer).Include(a => a.Lead);
    }

    public Task<PaginatedList<Activity>> GetPagedAsync(
        string? search, ActivityType? type, ActivityStatus? status, int page, int pageSize,
        string? userId, bool seeAll, CancellationToken ct = default)
    {
        var q = Scoped(userId, seeAll);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(a => a.Subject.Contains(s)
                || (a.Description != null && a.Description.Contains(s)));
        }

        if (type.HasValue)
            q = q.Where(a => a.ActivityType == type.Value);

        if (status.HasValue)
            q = q.Where(a => a.Status == status.Value);

        q = q.OrderByDescending(a => a.ActivityDate).ThenByDescending(a => a.CreatedDate);
        return PaginatedList<Activity>.CreateAsync(q, page, pageSize, ct);
    }

    public Task<Activity?> GetByIdAsync(int id, string? userId, bool seeAll, CancellationToken ct = default)
        => Scoped(userId, seeAll).FirstOrDefaultAsync(a => a.ActivityId == id, ct);

    public async Task<Activity> CreateAsync(
        Activity activity, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default)
    {
        activity.Subject = (activity.Subject ?? string.Empty).Trim();
        activity.CreatedById = userId;
        activity.CreatedDate = DateTime.UtcNow;

        await ValidateAsync(activity, userId, seeAll, ct);

        _db.Activities.Add(activity);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Create Activity", "Activity", activity.ActivityId.ToString(),
            newValue: Summarize(activity), userId: userId, userName: userName,
            ipAddress: ipAddress, ct: ct);

        return activity;
    }

    public async Task<Activity> UpdateAsync(
        Activity activity, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default)
    {
        var existing = await Scoped(userId, seeAll)
            .FirstOrDefaultAsync(a => a.ActivityId == activity.ActivityId, ct)
            ?? throw new CrmNotFoundException($"Activity {activity.ActivityId} not found.");

        var oldSummary = Summarize(existing);

        existing.ActivityType = activity.ActivityType;
        existing.Subject = (activity.Subject ?? string.Empty).Trim();
        existing.Description = activity.Description;
        existing.ActivityDate = activity.ActivityDate;
        existing.CustomerId = activity.CustomerId;
        existing.LeadId = activity.LeadId;
        existing.AssignedToId = activity.AssignedToId;
        existing.Status = activity.Status;

        await ValidateAsync(existing, userId, seeAll, ct);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Update Activity", "Activity", existing.ActivityId.ToString(),
            oldValue: oldSummary, newValue: Summarize(existing),
            userId: userId, userName: userName, ipAddress: ipAddress, ct: ct);

        return existing;
    }

    public async Task DeleteAsync(
        int id, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default)
    {
        var existing = await Scoped(userId, seeAll)
            .FirstOrDefaultAsync(a => a.ActivityId == id, ct)
            ?? throw new CrmNotFoundException($"Activity {id} not found.");

        var summary = Summarize(existing);
        _db.Activities.Remove(existing);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Delete Activity", "Activity", id.ToString(),
            oldValue: summary, userId: userId, userName: userName,
            ipAddress: ipAddress, ct: ct);
    }

    private async Task ValidateAsync(Activity a, string? userId, bool seeAll, CancellationToken ct)
    {
        var errors = new List<(string, string)>();

        if (string.IsNullOrWhiteSpace(a.Subject))
            errors.Add((nameof(Activity.Subject), "Subject is required."));

        if (a.ActivityDate == default)
            errors.Add((nameof(Activity.ActivityDate), "Activity date is required."));

        if (!Enum.IsDefined(a.ActivityType))
            errors.Add((nameof(Activity.ActivityType), "Invalid activity type."));

        if (!Enum.IsDefined(a.Status))
            errors.Add((nameof(Activity.Status), "Invalid activity status."));

        if (a.CustomerId.HasValue &&
            !await LookupQueries.CustomerVisibleAsync(_db, a.CustomerId.Value, userId, seeAll, ct))
            errors.Add((nameof(Activity.CustomerId),
                "The selected customer does not exist or you do not have access to it."));

        if (a.LeadId.HasValue &&
            !await LookupQueries.LeadVisibleAsync(_db, a.LeadId.Value, userId, seeAll, ct))
            errors.Add((nameof(Activity.LeadId),
                "The selected lead does not exist or you do not have access to it."));

        if (errors.Count > 0)
            throw new CrmValidationException(errors.ToArray());
    }

    private static string Summarize(Activity a)
        => $"Subject={a.Subject}; Type={a.ActivityType}; Date={a.ActivityDate:yyyy-MM-dd}; Status={a.Status}";
}
