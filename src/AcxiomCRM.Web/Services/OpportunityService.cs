using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Helpers;
using AcxiomCRM.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Services;

public class OpportunityService : IOpportunityService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    /// <summary>
    /// Legal stage transitions. Forward flow plus one step back,
    /// plus reopening a closed (Won/Lost) deal back to Qualification.
    /// </summary>
    private static readonly IReadOnlyDictionary<OpportunityStage, OpportunityStage[]> AllowedTransitions =
        new Dictionary<OpportunityStage, OpportunityStage[]>
        {
            [OpportunityStage.Qualification] = new[] { OpportunityStage.Proposal },
            [OpportunityStage.Proposal] = new[] { OpportunityStage.Qualification, OpportunityStage.Negotiation },
            [OpportunityStage.Negotiation] = new[] { OpportunityStage.Proposal, OpportunityStage.Won, OpportunityStage.Lost },
            [OpportunityStage.Won] = new[] { OpportunityStage.Qualification },
            [OpportunityStage.Lost] = new[] { OpportunityStage.Qualification },
        };

    public OpportunityService(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private IQueryable<Opportunity> Scoped(string? userId, bool seeAll)
    {
        var q = seeAll
            ? _db.Opportunities.AsQueryable()
            : _db.Opportunities.Where(o => o.AssignedToId == userId || o.CreatedById == userId);
        return q.Include(o => o.Customer);
    }

    public Task<PaginatedList<Opportunity>> GetPagedAsync(
        string? search, OpportunityStage? stage, int page, int pageSize,
        string? userId, bool seeAll, CancellationToken ct = default)
    {
        var q = Scoped(userId, seeAll);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(o => o.OpportunityName.Contains(s)
                || (o.Customer != null && o.Customer.CompanyName != null && o.Customer.CompanyName.Contains(s))
                || (o.Customer != null && o.Customer.CustomerName.Contains(s)));
        }

        if (stage.HasValue)
            q = q.Where(o => o.Stage == stage.Value);

        q = q.OrderByDescending(o => o.CreatedDate);
        return PaginatedList<Opportunity>.CreateAsync(q, page, pageSize, ct);
    }

    public Task<Opportunity?> GetByIdAsync(int id, string? userId, bool seeAll, CancellationToken ct = default)
        => Scoped(userId, seeAll)
            .Include(o => o.Lead)
            .FirstOrDefaultAsync(o => o.OpportunityId == id, ct);

    public async Task<Opportunity> CreateAsync(
        Opportunity opportunity, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default)
    {
        opportunity.OpportunityName = (opportunity.OpportunityName ?? string.Empty).Trim();
        opportunity.CreatedById = userId;
        opportunity.CreatedDate = DateTime.UtcNow;
        opportunity.ModifiedDate = null;

        await ValidateAsync(opportunity, userId, seeAll, ct);

        _db.Opportunities.Add(opportunity);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Create Opportunity", "Opportunity", opportunity.OpportunityId.ToString(),
            newValue: Summarize(opportunity), userId: userId, userName: userName,
            ipAddress: ipAddress, ct: ct);

        return opportunity;
    }

    public async Task<Opportunity> UpdateAsync(
        Opportunity opportunity, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default)
    {
        var existing = await Scoped(userId, seeAll)
            .FirstOrDefaultAsync(o => o.OpportunityId == opportunity.OpportunityId, ct)
            ?? throw new CrmNotFoundException($"Opportunity {opportunity.OpportunityId} not found.");

        var oldSummary = Summarize(existing);

        // Stage is deliberately NOT copied here: stage changes go through
        // ChangeStageAsync so the transition rules always hold.
        existing.OpportunityName = (opportunity.OpportunityName ?? string.Empty).Trim();
        existing.CustomerId = opportunity.CustomerId;
        existing.LeadId = opportunity.LeadId;
        existing.Amount = opportunity.Amount;
        existing.Probability = opportunity.Probability;
        existing.ExpectedCloseDate = opportunity.ExpectedCloseDate;
        existing.Source = opportunity.Source;
        existing.Notes = opportunity.Notes;
        existing.AssignedToId = opportunity.AssignedToId;
        existing.ModifiedDate = DateTime.UtcNow;

        await ValidateAsync(existing, userId, seeAll, ct);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Update Opportunity", "Opportunity", existing.OpportunityId.ToString(),
            oldValue: oldSummary, newValue: Summarize(existing),
            userId: userId, userName: userName, ipAddress: ipAddress, ct: ct);

        return existing;
    }

    public async Task DeleteAsync(
        int id, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default)
    {
        var existing = await Scoped(userId, seeAll)
            .FirstOrDefaultAsync(o => o.OpportunityId == id, ct)
            ?? throw new CrmNotFoundException($"Opportunity {id} not found.");

        var summary = Summarize(existing);
        _db.Opportunities.Remove(existing);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Delete Opportunity", "Opportunity", id.ToString(),
            oldValue: summary, userId: userId, userName: userName,
            ipAddress: ipAddress, ct: ct);
    }

    public async Task<Opportunity> ChangeStageAsync(
        int id, OpportunityStage newStage, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(newStage))
            throw new CrmValidationException((nameof(Opportunity.Stage), "Invalid stage."));

        var existing = await Scoped(userId, seeAll)
            .FirstOrDefaultAsync(o => o.OpportunityId == id, ct)
            ?? throw new CrmNotFoundException($"Opportunity {id} not found.");

        if (existing.Stage == newStage)
            return existing; // no-op, nothing to audit

        if (!AllowedTransitions.TryGetValue(existing.Stage, out var allowed) || !allowed.Contains(newStage))
            throw new CrmValidationException((nameof(Opportunity.Stage),
                $"Cannot move an opportunity from {existing.Stage} to {newStage}."));

        if (newStage == OpportunityStage.Won && existing.Amount <= 0)
            throw new CrmValidationException((nameof(Opportunity.Amount),
                "An opportunity must have an Amount greater than zero before it can be marked Won."));

        var oldStage = existing.Stage;
        existing.Stage = newStage;
        existing.ModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Opportunity Stage Changed", "Opportunity", existing.OpportunityId.ToString(),
            oldValue: oldStage.ToString(), newValue: newStage.ToString(),
            userId: userId, userName: userName, ipAddress: ipAddress, ct: ct);

        return existing;
    }

    public async Task<IReadOnlyList<OpportunityPipelineStage>> GetPipelineAsync(
        string? userId, bool seeAll, CancellationToken ct = default)
    {
        // SQLite cannot ORDER BY decimal columns; the set is small, so materialize
        // first and order in memory (works on SQL Server too).
        var all = await Scoped(userId, seeAll).ToListAsync(ct);

        var stages = new List<OpportunityPipelineStage>();
        foreach (var stage in Enum.GetValues<OpportunityStage>())
        {
            stages.Add(new OpportunityPipelineStage
            {
                Stage = stage,
                Opportunities = all.Where(o => o.Stage == stage)
                    .OrderByDescending(o => o.Amount)
                    .ToList()
            });
        }
        return stages;
    }

    private async Task ValidateAsync(Opportunity o, string? userId, bool seeAll, CancellationToken ct)
    {
        var errors = new List<(string, string)>();

        if (string.IsNullOrWhiteSpace(o.OpportunityName))
            errors.Add((nameof(Opportunity.OpportunityName), "Opportunity Name is required."));

        if (!Enum.IsDefined(o.Stage))
            errors.Add((nameof(Opportunity.Stage), "Invalid stage."));

        if (o.Probability < 0 || o.Probability > 100)
            errors.Add((nameof(Opportunity.Probability), "Probability must be between 0 and 100."));

        var isOpen = o.Stage != OpportunityStage.Won && o.Stage != OpportunityStage.Lost;

        if (isOpen && o.Amount <= 0)
            errors.Add((nameof(Opportunity.Amount), "Amount must be greater than zero for open opportunities."));
        if (!isOpen && o.Amount < 0)
            errors.Add((nameof(Opportunity.Amount), "Amount cannot be negative."));

        if (isOpen && o.ExpectedCloseDate.Date < DateTime.Today)
            errors.Add((nameof(Opportunity.ExpectedCloseDate), "Expected Close Date cannot be in the past."));

        if (o.CustomerId.HasValue &&
            !await LookupQueries.CustomerVisibleAsync(_db, o.CustomerId.Value, userId, seeAll, ct))
            errors.Add((nameof(Opportunity.CustomerId),
                "The selected customer does not exist or you do not have access to it."));

        if (o.LeadId.HasValue &&
            !await LookupQueries.LeadVisibleAsync(_db, o.LeadId.Value, userId, seeAll, ct))
            errors.Add((nameof(Opportunity.LeadId),
                "The selected lead does not exist or you do not have access to it."));

        if (errors.Count > 0)
            throw new CrmValidationException(errors.ToArray());
    }

    private static string Summarize(Opportunity o)
        => $"Name={o.OpportunityName}; Amount={o.Amount}; Stage={o.Stage}; Prob={o.Probability}%";
}
