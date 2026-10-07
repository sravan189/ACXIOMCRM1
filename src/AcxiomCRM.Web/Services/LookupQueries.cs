using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Services;

/// <summary>
/// Shared visibility-scoped lookup queries used by the CRM services and controllers
/// for customer/lead/assignee dropdowns.
/// </summary>
internal static class LookupQueries
{
    public static IQueryable<Customer> ScopedCustomers(ApplicationDbContext db, string? userId, bool seeAll)
        => seeAll
            ? db.Customers
            : db.Customers.Where(c => c.AssignedToId == userId || c.CreatedById == userId);

    public static IQueryable<Lead> ScopedLeads(ApplicationDbContext db, string? userId, bool seeAll)
        => seeAll
            ? db.Leads
            : db.Leads.Where(l => l.AssignedToId == userId || l.CreatedById == userId);

    public static Task<bool> CustomerVisibleAsync(ApplicationDbContext db, int customerId, string? userId, bool seeAll, CancellationToken ct)
        => ScopedCustomers(db, userId, seeAll).AnyAsync(c => c.CustomerId == customerId, ct);

    public static Task<bool> LeadVisibleAsync(ApplicationDbContext db, int leadId, string? userId, bool seeAll, CancellationToken ct)
        => ScopedLeads(db, userId, seeAll).AnyAsync(l => l.LeadId == leadId, ct);

    public static Task<List<SelectListItem>> CustomerOptionsAsync(ApplicationDbContext db, string? userId, bool seeAll, CancellationToken ct)
        => ScopedCustomers(db, userId, seeAll)
            .OrderBy(c => c.CompanyName)
            .ThenBy(c => c.CustomerName)
            .Select(c => new SelectListItem
            {
                Value = c.CustomerId.ToString(),
                Text = (c.CompanyName ?? c.CustomerName) + " (" + c.CustomerCode + ")"
            })
            .ToListAsync(ct);

    public static Task<List<SelectListItem>> LeadOptionsAsync(ApplicationDbContext db, string? userId, bool seeAll, CancellationToken ct)
        => ScopedLeads(db, userId, seeAll)
            .OrderBy(l => l.CompanyName)
            .ThenBy(l => l.LeadName)
            .Select(l => new SelectListItem
            {
                Value = l.LeadId.ToString(),
                Text = (l.CompanyName ?? l.LeadName) + " (" + l.LeadCode + ")"
            })
            .ToListAsync(ct);

    public static Task<List<SelectListItem>> UserOptionsAsync(ApplicationDbContext db, CancellationToken ct)
        => db.Users
            .OrderBy(u => u.UserName)
            .Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = string.IsNullOrEmpty(u.Email) || u.Email == u.UserName
                    ? (u.UserName ?? u.Id)
                    : (u.UserName ?? u.Id) + " (" + u.Email + ")"
            })
            .ToListAsync(ct);
}
