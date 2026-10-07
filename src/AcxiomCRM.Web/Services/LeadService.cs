using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Helpers;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Services;

/// <summary>Lead status state machine. Converted/Lost are terminal.</summary>
public static class LeadStatusTransitions
{
    private static readonly Dictionary<LeadStatus, LeadStatus[]> Allowed = new()
    {
        [LeadStatus.New] = new[] { LeadStatus.Contacted, LeadStatus.Unqualified, LeadStatus.Lost },
        [LeadStatus.Contacted] = new[] { LeadStatus.Qualified, LeadStatus.Unqualified, LeadStatus.Lost },
        // Qualified -> Converted is only allowed through ConvertAsync.
        [LeadStatus.Qualified] = new[] { LeadStatus.Unqualified, LeadStatus.Lost },
        [LeadStatus.Unqualified] = new[] { LeadStatus.New, LeadStatus.Contacted, LeadStatus.Lost },
        [LeadStatus.Converted] = Array.Empty<LeadStatus>(),
        [LeadStatus.Lost] = Array.Empty<LeadStatus>(),
    };

    public static IReadOnlyList<LeadStatus> GetAllowed(LeadStatus current)
        => Allowed.TryGetValue(current, out var next) ? next : Array.Empty<LeadStatus>();

    public static bool IsAllowed(LeadStatus current, LeadStatus next)
        => GetAllowed(current).Contains(next);
}

public class LeadService : ILeadService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly UserManager<ApplicationUser> _userManager;
    private static readonly EmailAddressAttribute EmailFormat = new();
    private static readonly IndianPhoneAttribute PhoneFormat = new();

    public LeadService(
        ApplicationDbContext db,
        IAuditService audit,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _audit = audit;
        _userManager = userManager;
    }

    public async Task<PaginatedList<Lead>> GetPagedAsync(
        string? search, LeadStatus? status, string? assignedTo, int page, int pageSize,
        string? userId, bool seeAll, string? sortBy = null, string? sortDir = null)
    {
        var query = _db.Leads.AsNoTracking()
            .Include(l => l.AssignedTo)
            .AsQueryable();

        if (!seeAll)
            query = query.Where(l => l.AssignedToId == userId || l.CreatedById == userId);

        if (status.HasValue)
            query = query.Where(l => l.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(assignedTo))
            query = query.Where(l => l.AssignedToId == assignedTo);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(l =>
                l.LeadName.Contains(s) ||
                (l.CompanyName != null && l.CompanyName.Contains(s)));
        }

        query = ApplySort(query, sortBy, sortDir);
        return await PaginatedList<Lead>.CreateAsync(query, Math.Max(1, page), pageSize);
    }

    public async Task<Lead?> GetByIdAsync(int id, string? userId, bool seeAll)
    {
        var query = _db.Leads.AsNoTracking()
            .Include(l => l.FollowUps)
            .Include(l => l.Activities)
            .Include(l => l.CreatedBy)
            .Include(l => l.AssignedTo)
            .AsQueryable();

        if (!seeAll)
            query = query.Where(l => l.AssignedToId == userId || l.CreatedById == userId);

        return await query.FirstOrDefaultAsync(l => l.LeadId == id);
    }

    public async Task<(bool ok, string? error, Lead? lead)> CreateAsync(
        Lead entity, string? userId, string? userName, string? ip)
    {
        Normalize(entity);

        var validationError = Validate(entity);
        if (validationError is not null)
            return (false, validationError, null);

        var maxId = await _db.Leads.MaxAsync(l => (int?)l.LeadId) ?? 0;
        entity.LeadCode = "LEAD-" + (maxId + 1).ToString("D4");
        entity.Status = LeadStatus.New;
        entity.CreatedDate = DateTime.UtcNow;
        entity.CreatedById = userId;

        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.Leads.Add(entity);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create Lead", "Lead", entity.LeadId.ToString(),
            null, Summary(entity), userId, userName, ip);
        await tx.CommitAsync();

        return (true, null, entity);
    }

    public async Task<(bool ok, string? error, Lead? lead)> UpdateAsync(
        Lead entity, string? userId, string? userName, string? ip)
    {
        var existing = await _db.Leads.FirstOrDefaultAsync(l => l.LeadId == entity.LeadId);
        if (existing is null)
            return (false, "Lead not found.", null);

        Normalize(entity);

        var validationError = Validate(entity);
        if (validationError is not null)
            return (false, validationError, null);

        var oldSummary = Summary(existing);

        // Status, LeadCode, assignment and ownership are managed exclusively
        // through ChangeStatusAsync / ConvertAsync / AssignAsync — never via Edit.
        existing.LeadName = entity.LeadName;
        existing.Email = entity.Email;
        existing.Phone = entity.Phone;
        existing.CompanyName = entity.CompanyName;
        existing.Source = entity.Source;
        existing.Priority = entity.Priority;
        existing.ExpectedValue = entity.ExpectedValue;
        existing.Notes = entity.Notes;

        await using var tx = await _db.Database.BeginTransactionAsync();
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update Lead", "Lead", existing.LeadId.ToString(),
            oldSummary, Summary(existing), userId, userName, ip);
        await tx.CommitAsync();

        return (true, null, existing);
    }

    public async Task<(bool ok, string? error)> DeleteAsync(
        int id, string? userId, string? userName, string? ip)
    {
        var existing = await _db.Leads.FirstOrDefaultAsync(l => l.LeadId == id);
        if (existing is null)
            return (false, "Lead not found.");

        var snapshot = Summary(existing);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Leads.Remove(existing);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            return (false, "This lead has related records (follow-ups, activities or opportunities) and cannot be deleted.");
        }

        await _audit.LogAsync("Delete Lead", "Lead", id.ToString(),
            snapshot, null, userId, userName, ip);
        await tx.CommitAsync();

        return (true, null);
    }

    public async Task<(bool ok, string? error, Lead? lead)> AssignAsync(
        int id, string assignToUserId, string? userId, string? userName, string? ip)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.LeadId == id);
        if (lead is null)
            return (false, "Lead not found.", null);

        var target = await _userManager.FindByIdAsync(assignToUserId);
        if (target is null || !target.IsActive)
            return (false, "The selected user was not found.", null);

        if (!await _userManager.IsInRoleAsync(target, RoleNames.SalesExecutive) &&
            !await _userManager.IsInRoleAsync(target, RoleNames.Manager))
            return (false, "Leads can only be assigned to a Sales Executive or Manager.", null);

        if (lead.AssignedToId == assignToUserId)
            return (false, "Lead is already assigned to this user.", null);

        var oldAssignee = lead.AssignedToId;
        lead.AssignedToId = assignToUserId;

        await using var tx = await _db.Database.BeginTransactionAsync();
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Lead Assigned", "Lead", lead.LeadId.ToString(),
            JsonSerializer.Serialize(new { lead.LeadCode, AssignedTo = oldAssignee }),
            JsonSerializer.Serialize(new { lead.LeadCode, AssignedTo = assignToUserId, AssigneeName = target.FullName }),
            userId, userName, ip);
        await tx.CommitAsync();

        return (true, null, lead);
    }

    public async Task<(bool ok, string? error, Lead? lead)> ChangeStatusAsync(
        int id, LeadStatus newStatus, string? userId, string? userName, string? ip)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.LeadId == id);
        if (lead is null)
            return (false, "Lead not found.", null);

        if (lead.Status == newStatus)
            return (false, $"Lead is already {newStatus}.", null);

        if (newStatus == LeadStatus.Converted)
            return (false, "A lead can only be marked Converted through the Convert action.", null);

        if (!LeadStatusTransitions.IsAllowed(lead.Status, newStatus))
            return (false, $"Cannot move lead from {lead.Status} to {newStatus}.", null);

        var oldStatus = lead.Status;
        lead.Status = newStatus;

        await using var tx = await _db.Database.BeginTransactionAsync();
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Lead Status Changed", "Lead", lead.LeadId.ToString(),
            JsonSerializer.Serialize(new { lead.LeadCode, Status = oldStatus.ToString() }),
            JsonSerializer.Serialize(new { lead.LeadCode, Status = newStatus.ToString() }),
            userId, userName, ip);
        await tx.CommitAsync();

        return (true, null, lead);
    }

    public async Task<(bool ok, string? error, Customer? customer, Opportunity? opportunity)> ConvertAsync(
        int id, bool createOpportunity, string? opportunityName,
        decimal opportunityAmount, int probability, DateTime? expectedCloseDate,
        string? userId, string? userName, string? ip)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.LeadId == id);
        if (lead is null)
            return (false, "Lead not found.", null, null);

        if (lead.Status != LeadStatus.Qualified)
            return (false, "Only qualified leads can be converted.", null, null);

        if (string.IsNullOrWhiteSpace(lead.Email) || !EmailFormat.IsValid(lead.Email.Trim()))
            return (false, "Cannot convert: the lead must have a valid email address.", null, null);

        if (string.IsNullOrWhiteSpace(lead.Phone) || !PhoneFormat.IsValid(NormalizePhone(lead.Phone)))
            return (false, "Cannot convert: the lead must have a valid phone number.", null, null);

        var email = lead.Email.Trim().ToLowerInvariant();
        var phone = NormalizePhone(lead.Phone);

        if (await _db.Customers.AnyAsync(c => c.Email.ToLower() == email))
            return (false, "A customer with this email/phone already exists.", null, null);
        if (await _db.Customers.AnyAsync(c => c.Phone == phone))
            return (false, "A customer with this email/phone already exists.", null, null);

        if (createOpportunity)
        {
            if (string.IsNullOrWhiteSpace(opportunityName))
                return (false, "Opportunity name is required.", null, null);
            if (opportunityAmount <= 0)
                return (false, "Opportunity amount must be greater than zero.", null, null);
            if (probability < 0 || probability > 100)
                return (false, "Probability must be between 0 and 100.", null, null);
            if (!expectedCloseDate.HasValue)
                return (false, "Expected close date is required.", null, null);
            if (expectedCloseDate.Value.Date < DateTime.Today)
                return (false, "Expected close date cannot be in the past.", null, null);
        }

        var maxCustId = await _db.Customers.MaxAsync(c => (int?)c.CustomerId) ?? 0;
        var customer = new Customer
        {
            CustomerCode = "CUST-" + (maxCustId + 1).ToString("D4"),
            CustomerName = lead.LeadName.Trim(),
            Email = lead.Email.Trim(),
            Phone = phone,
            CompanyName = string.IsNullOrWhiteSpace(lead.CompanyName) ? null : lead.CompanyName.Trim(),
            Status = CustomerStatus.Active,
            CreatedDate = DateTime.UtcNow,
            CreatedById = userId,
            AssignedToId = lead.AssignedToId ?? userId,
        };

        Opportunity? opportunity = null;

        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(); // materialize CustomerId for the opportunity

        if (createOpportunity)
        {
            opportunity = new Opportunity
            {
                OpportunityName = opportunityName!.Trim(),
                CustomerId = customer.CustomerId,
                LeadId = lead.LeadId,
                Amount = opportunityAmount,
                Stage = OpportunityStage.Qualification,
                Probability = probability,
                ExpectedCloseDate = expectedCloseDate!.Value,
                Source = string.IsNullOrWhiteSpace(lead.Source) ? "Lead Conversion" : lead.Source,
                Notes = $"Created from lead {lead.LeadCode}.",
                CreatedDate = DateTime.UtcNow,
                CreatedById = userId,
                AssignedToId = lead.AssignedToId ?? userId,
            };
            _db.Opportunities.Add(opportunity);
        }

        lead.Status = LeadStatus.Converted;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Lead Converted", "Lead", lead.LeadId.ToString(),
            JsonSerializer.Serialize(new { lead.LeadCode, Status = "Qualified" }),
            JsonSerializer.Serialize(new
            {
                lead.LeadCode,
                Status = "Converted",
                CustomerCode = customer.CustomerCode,
                Opportunity = opportunity?.OpportunityName,
                OpportunityAmount = opportunity?.Amount
            }),
            userId, userName, ip);
        await tx.CommitAsync();

        return (true, null, customer, opportunity);
    }

    public async Task<List<Opportunity>> GetOpportunitiesForLeadAsync(int leadId, string? userId, bool seeAll)
    {
        var leadVisible = await _db.Leads.AsNoTracking().AnyAsync(l =>
            l.LeadId == leadId && (seeAll || l.AssignedToId == userId || l.CreatedById == userId));

        if (!leadVisible)
            return new List<Opportunity>();

        return await _db.Opportunities.AsNoTracking()
            .Where(o => o.LeadId == leadId)
            .OrderByDescending(o => o.CreatedDate)
            .ToListAsync();
    }

    // ---------- helpers ----------

    private static IQueryable<Lead> ApplySort(IQueryable<Lead> query, string? sortBy, string? sortDir)
    {
        var desc = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        return sortBy?.ToLowerInvariant() switch
        {
            "name" => desc
                ? query.OrderByDescending(l => l.LeadName)
                : query.OrderBy(l => l.LeadName),
            _ => desc
                ? query.OrderByDescending(l => l.CreatedDate)
                : query.OrderBy(l => l.CreatedDate),
        };
    }

    /// <summary>
    /// Server-side validation (independent of any client validation):
    /// name required, email/phone valid when supplied, expected value &gt;= 0.
    /// </summary>
    private static string? Validate(Lead entity)
    {
        if (string.IsNullOrWhiteSpace(entity.LeadName))
            return "Lead Name is required.";
        if (!string.IsNullOrWhiteSpace(entity.Email) && !EmailFormat.IsValid(entity.Email.Trim()))
            return "Enter a valid email address.";
        if (!string.IsNullOrWhiteSpace(entity.Phone) && !PhoneFormat.IsValid(NormalizePhone(entity.Phone)))
            return "Enter a valid 10-digit Indian mobile number.";
        if (entity.ExpectedValue < 0)
            return "Expected Value must be 0 or greater.";
        return null;
    }

    private static void Normalize(Lead entity)
    {
        entity.LeadName = entity.LeadName?.Trim() ?? string.Empty;
        entity.Email = string.IsNullOrWhiteSpace(entity.Email) ? null : entity.Email.Trim();
        entity.Phone = string.IsNullOrWhiteSpace(entity.Phone) ? null : NormalizePhone(entity.Phone);
        entity.CompanyName = string.IsNullOrWhiteSpace(entity.CompanyName) ? null : entity.CompanyName.Trim();
        entity.Source = string.IsNullOrWhiteSpace(entity.Source) ? null : entity.Source.Trim();
        entity.Notes = string.IsNullOrWhiteSpace(entity.Notes) ? null : entity.Notes.Trim();
    }

    private static string NormalizePhone(string? phone)
    {
        var digits = Regex.Replace(phone ?? string.Empty, @"[\s\-+]", string.Empty);
        if (digits.StartsWith("91") && digits.Length == 12)
            digits = digits[2..];
        return digits;
    }

    private static string Summary(Lead l) => JsonSerializer.Serialize(new
    {
        l.LeadCode,
        l.LeadName,
        l.Email,
        l.Phone,
        l.CompanyName,
        l.Source,
        Status = l.Status.ToString(),
        Priority = l.Priority.ToString(),
        l.ExpectedValue
    });
}
