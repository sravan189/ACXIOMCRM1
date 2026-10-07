using AcxiomCRM.Web.Helpers;
using AcxiomCRM.Web.Models;

namespace AcxiomCRM.Web.Services;

/// <summary>
/// Lead CRUD, assignment, status workflow and conversion to customer,
/// with SalesExecutive visibility scoping and an audit entry on every mutation.
/// </summary>
public interface ILeadService
{
    Task<PaginatedList<Lead>> GetPagedAsync(
        string? search,
        LeadStatus? status,
        string? assignedTo,
        int page,
        int pageSize,
        string? userId,
        bool seeAll,
        string? sortBy = null,
        string? sortDir = null);

    /// <summary>Returns null when the record does not exist or is not visible to the user.</summary>
    Task<Lead?> GetByIdAsync(int id, string? userId, bool seeAll);

    Task<(bool ok, string? error, Lead? lead)> CreateAsync(
        Lead entity, string? userId, string? userName, string? ip);

    Task<(bool ok, string? error, Lead? lead)> UpdateAsync(
        Lead entity, string? userId, string? userName, string? ip);

    /// <summary>Hard delete (blocked when related records exist). Audited as "Delete Lead".</summary>
    Task<(bool ok, string? error)> DeleteAsync(
        int id, string? userId, string? userName, string? ip);

    /// <summary>Assign to a SalesExecutive/Manager user. Audited as "Lead Assigned".</summary>
    Task<(bool ok, string? error, Lead? lead)> AssignAsync(
        int id, string assignToUserId, string? userId, string? userName, string? ip);

    /// <summary>
    /// Change status enforcing the lead state machine. Converted/Lost are terminal;
    /// Qualified -&gt; Converted is only allowed through <see cref="ConvertAsync"/>.
    /// </summary>
    Task<(bool ok, string? error, Lead? lead)> ChangeStatusAsync(
        int id, LeadStatus newStatus, string? userId, string? userName, string? ip);

    /// <summary>
    /// Convert a Qualified lead into a Customer (new CUST- code), optionally creating
    /// a linked Opportunity. All in one transaction. Audited as "Lead Converted".
    /// </summary>
    Task<(bool ok, string? error, Customer? customer, Opportunity? opportunity)> ConvertAsync(
        int id,
        bool createOpportunity,
        string? opportunityName,
        decimal opportunityAmount,
        int probability,
        DateTime? expectedCloseDate,
        string? userId,
        string? userName,
        string? ip);

    /// <summary>Opportunities linked to a lead (only when the lead is visible to the user).</summary>
    Task<List<Opportunity>> GetOpportunitiesForLeadAsync(int leadId, string? userId, bool seeAll);
}
