using AcxiomCRM.Web.Helpers;
using AcxiomCRM.Web.Models;

namespace AcxiomCRM.Web.Services;

/// <summary>
/// Customer CRUD, search/paging and history, with SalesExecutive visibility
/// scoping and an audit entry on every mutation.
/// </summary>
public interface ICustomerService
{
    Task<PaginatedList<Customer>> GetPagedAsync(
        string? search,
        CustomerStatus? status,
        int page,
        int pageSize,
        string? userId,
        bool seeAll,
        string? sortBy = null,
        string? sortDir = null);

    /// <summary>Returns null when the record does not exist or is not visible to the user.</summary>
    Task<Customer?> GetByIdAsync(int id, string? userId, bool seeAll);

    /// <summary>Customer with Opportunities, FollowUps and Activities included. Null when not visible.</summary>
    Task<Customer?> GetHistoryAsync(int id, string? userId, bool seeAll);

    Task<(bool ok, string? error, Customer? customer)> CreateAsync(
        Customer entity, string? userId, string? userName, string? ip);

    Task<(bool ok, string? error, Customer? customer)> UpdateAsync(
        Customer entity, string? userId, string? userName, string? ip);

    /// <summary>Soft delete: sets Status = Inactive. Audited as "Delete Customer".</summary>
    Task<(bool ok, string? error)> DeactivateAsync(
        int id, string? userId, string? userName, string? ip);
}
