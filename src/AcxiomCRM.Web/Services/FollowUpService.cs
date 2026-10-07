using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Helpers;
using AcxiomCRM.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Services;

public class FollowUpService : IFollowUpService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public FollowUpService(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private IQueryable<FollowUp> Scoped(string? userId, bool seeAll)
    {
        var q = seeAll
            ? _db.FollowUps.AsQueryable()
            : _db.FollowUps.Where(f => f.AssignedToId == userId || f.CreatedById == userId);
        return q.Include(f => f.Customer).Include(f => f.Lead);
    }

    public Task<PaginatedList<FollowUp>> GetPagedAsync(
        string? filter, string? search, string? assignedTo, int page, int pageSize,
        string? userId, bool seeAll, CancellationToken ct = default)
    {
        var q = Scoped(userId, seeAll);
        var today = DateTime.Today;

        q = (filter ?? "upcoming").Trim().ToLowerInvariant() switch
        {
            "overdue" => q.Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate.Date < today)
                          .OrderBy(f => f.FollowUpDate),
            "completed" => q.Where(f => f.Status != FollowUpStatus.Planned)
                            .OrderByDescending(f => f.FollowUpDate),
            _ => q.Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate.Date >= today)
                  .OrderBy(f => f.FollowUpDate),
        };

        if (!seeAll)
            assignedTo = userId; // SalesExecutive may only see their own queue
        if (!string.IsNullOrWhiteSpace(assignedTo))
            q = q.Where(f => f.AssignedToId == assignedTo);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(f => (f.Remarks != null && f.Remarks.Contains(s))
                || (f.Customer != null && f.Customer.CompanyName != null && f.Customer.CompanyName.Contains(s))
                || (f.Customer != null && f.Customer.CustomerName.Contains(s))
                || (f.Lead != null && f.Lead.CompanyName != null && f.Lead.CompanyName.Contains(s))
                || (f.Lead != null && f.Lead.LeadName.Contains(s)));
        }

        return PaginatedList<FollowUp>.CreateAsync(q, page, pageSize, ct);
    }

    public Task<FollowUp?> GetByIdAsync(int id, string? userId, bool seeAll, CancellationToken ct = default)
        => Scoped(userId, seeAll).FirstOrDefaultAsync(f => f.FollowUpId == id, ct);

    public async Task<FollowUp> CreateAsync(
        FollowUp followUp, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default)
    {
        followUp.CreatedById = userId;
        followUp.CreatedDate = DateTime.UtcNow;

        await ValidateAsync(followUp, userId, seeAll, ct);

        _db.FollowUps.Add(followUp);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Create Follow-up", "FollowUp", followUp.FollowUpId.ToString(),
            newValue: Summarize(followUp), userId: userId, userName: userName,
            ipAddress: ipAddress, ct: ct);

        return followUp;
    }

    public async Task<FollowUp> UpdateAsync(
        FollowUp followUp, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default)
    {
        var existing = await Scoped(userId, seeAll)
            .FirstOrDefaultAsync(f => f.FollowUpId == followUp.FollowUpId, ct)
            ?? throw new CrmNotFoundException($"Follow-up {followUp.FollowUpId} not found.");

        var oldSummary = Summarize(existing);

        existing.CustomerId = followUp.CustomerId;
        existing.LeadId = followUp.LeadId;
        existing.FollowUpDate = followUp.FollowUpDate;
        existing.FollowUpType = followUp.FollowUpType;
        existing.Remarks = followUp.Remarks;
        existing.Status = followUp.Status;
        existing.AssignedToId = followUp.AssignedToId;

        await ValidateAsync(existing, userId, seeAll, ct);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Update Follow-up", "FollowUp", existing.FollowUpId.ToString(),
            oldValue: oldSummary, newValue: Summarize(existing),
            userId: userId, userName: userName, ipAddress: ipAddress, ct: ct);

        return existing;
    }

    public async Task DeleteAsync(
        int id, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default)
    {
        var existing = await Scoped(userId, seeAll)
            .FirstOrDefaultAsync(f => f.FollowUpId == id, ct)
            ?? throw new CrmNotFoundException($"Follow-up {id} not found.");

        var summary = Summarize(existing);
        _db.FollowUps.Remove(existing);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Delete Follow-up", "FollowUp", id.ToString(),
            oldValue: summary, userId: userId, userName: userName,
            ipAddress: ipAddress, ct: ct);
    }

    public async Task<FollowUp> CompleteAsync(
        int id, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default)
    {
        var existing = await Scoped(userId, seeAll)
            .FirstOrDefaultAsync(f => f.FollowUpId == id, ct)
            ?? throw new CrmNotFoundException($"Follow-up {id} not found.");

        if (existing.Status != FollowUpStatus.Planned && existing.Status != FollowUpStatus.Missed)
            throw new CrmValidationException((nameof(FollowUp.Status),
                "Only planned or missed follow-ups can be completed."));

        var oldStatus = existing.Status;
        existing.Status = FollowUpStatus.Completed;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Follow-up Completed", "FollowUp", existing.FollowUpId.ToString(),
            oldValue: oldStatus.ToString(), newValue: FollowUpStatus.Completed.ToString(),
            userId: userId, userName: userName, ipAddress: ipAddress, ct: ct);

        return existing;
    }

    public async Task<FollowUp> RescheduleAsync(
        int id, DateTime newDate, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default)
    {
        var existing = await Scoped(userId, seeAll)
            .FirstOrDefaultAsync(f => f.FollowUpId == id, ct)
            ?? throw new CrmNotFoundException($"Follow-up {id} not found.");

        if (existing.Status == FollowUpStatus.Completed)
            throw new CrmValidationException((nameof(FollowUp.FollowUpDate),
                "A completed follow-up cannot be rescheduled."));

        if (newDate == default)
            throw new CrmValidationException((nameof(FollowUp.FollowUpDate),
                "Follow-up date is required."));

        if (newDate.Date < DateTime.Today)
            throw new CrmValidationException((nameof(FollowUp.FollowUpDate),
                "Follow-up date cannot be earlier than today."));

        var oldDate = existing.FollowUpDate;
        var oldStatus = existing.Status;
        existing.FollowUpDate = newDate;
        existing.Status = FollowUpStatus.Planned; // rescheduling re-opens a missed/cancelled one
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Follow-up Rescheduled", "FollowUp", existing.FollowUpId.ToString(),
            oldValue: $"{oldDate:yyyy-MM-dd} ({oldStatus})",
            newValue: $"{newDate:yyyy-MM-dd} ({FollowUpStatus.Planned})",
            userId: userId, userName: userName, ipAddress: ipAddress, ct: ct);

        return existing;
    }

    public async Task<FollowUp> CancelAsync(
        int id, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default)
    {
        var existing = await Scoped(userId, seeAll)
            .FirstOrDefaultAsync(f => f.FollowUpId == id, ct)
            ?? throw new CrmNotFoundException($"Follow-up {id} not found.");

        if (existing.Status == FollowUpStatus.Completed)
            throw new CrmValidationException((nameof(FollowUp.Status),
                "A completed follow-up cannot be cancelled."));

        var oldStatus = existing.Status;
        existing.Status = FollowUpStatus.Cancelled;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Follow-up Cancelled", "FollowUp", existing.FollowUpId.ToString(),
            oldValue: oldStatus.ToString(), newValue: FollowUpStatus.Cancelled.ToString(),
            userId: userId, userName: userName, ipAddress: ipAddress, ct: ct);

        return existing;
    }

    private async Task ValidateAsync(FollowUp f, string? userId, bool seeAll, CancellationToken ct)
    {
        var errors = new List<(string, string)>();

        if (f.FollowUpDate == default)
            errors.Add((nameof(FollowUp.FollowUpDate), "Follow-up date is required."));

        if (!Enum.IsDefined(f.FollowUpType))
            errors.Add((nameof(FollowUp.FollowUpType), "Invalid follow-up type."));

        if (!Enum.IsDefined(f.Status))
            errors.Add((nameof(FollowUp.Status), "Invalid follow-up status."));

        // Planned (i.e. not yet done) follow-ups may not be scheduled in the past.
        if (f.Status == FollowUpStatus.Planned && f.FollowUpDate != default
            && f.FollowUpDate.Date < DateTime.Today)
            errors.Add((nameof(FollowUp.FollowUpDate),
                "Follow-up date cannot be earlier than today."));

        if (f.CustomerId.HasValue &&
            !await LookupQueries.CustomerVisibleAsync(_db, f.CustomerId.Value, userId, seeAll, ct))
            errors.Add((nameof(FollowUp.CustomerId),
                "The selected customer does not exist or you do not have access to it."));

        if (f.LeadId.HasValue &&
            !await LookupQueries.LeadVisibleAsync(_db, f.LeadId.Value, userId, seeAll, ct))
            errors.Add((nameof(FollowUp.LeadId),
                "The selected lead does not exist or you do not have access to it."));

        if (errors.Count > 0)
            throw new CrmValidationException(errors.ToArray());
    }

    private static string Summarize(FollowUp f)
        => $"Date={f.FollowUpDate:yyyy-MM-dd}; Type={f.FollowUpType}; Status={f.Status}";
}
