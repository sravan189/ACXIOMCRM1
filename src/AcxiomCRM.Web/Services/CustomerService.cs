using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Helpers;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Validation;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Services;

public class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private static readonly EmailAddressAttribute EmailFormat = new();
    private static readonly IndianPhoneAttribute PhoneFormat = new();

    public CustomerService(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<PaginatedList<Customer>> GetPagedAsync(
        string? search, CustomerStatus? status, int page, int pageSize,
        string? userId, bool seeAll, string? sortBy = null, string? sortDir = null)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();

        if (!seeAll)
            query = query.Where(c => c.AssignedToId == userId || c.CreatedById == userId);

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(c =>
                c.CustomerName.Contains(s) ||
                c.Email.Contains(s) ||
                c.Phone.Contains(s) ||
                (c.CompanyName != null && c.CompanyName.Contains(s)));
        }

        query = ApplySort(query, sortBy, sortDir);
        return await PaginatedList<Customer>.CreateAsync(query, Math.Max(1, page), pageSize);
    }

    public async Task<Customer?> GetByIdAsync(int id, string? userId, bool seeAll)
    {
        var query = _db.Customers.AsNoTracking()
            .Include(c => c.CreatedBy)
            .Include(c => c.AssignedTo)
            .AsQueryable();

        if (!seeAll)
            query = query.Where(c => c.AssignedToId == userId || c.CreatedById == userId);

        return await query.FirstOrDefaultAsync(c => c.CustomerId == id);
    }

    public async Task<Customer?> GetHistoryAsync(int id, string? userId, bool seeAll)
    {
        var query = _db.Customers.AsNoTracking()
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps)
            .Include(c => c.Activities)
            .Include(c => c.CreatedBy)
            .Include(c => c.AssignedTo)
            .AsQueryable();

        if (!seeAll)
            query = query.Where(c => c.AssignedToId == userId || c.CreatedById == userId);

        return await query.FirstOrDefaultAsync(c => c.CustomerId == id);
    }

    public async Task<(bool ok, string? error, Customer? customer)> CreateAsync(
        Customer entity, string? userId, string? userName, string? ip)
    {
        Normalize(entity);

        var validationError = await ValidateAsync(entity, excludeId: null);
        if (validationError is not null)
            return (false, validationError, null);

        var maxId = await _db.Customers.MaxAsync(c => (int?)c.CustomerId) ?? 0;
        entity.CustomerCode = "CUST-" + (maxId + 1).ToString("D4");
        entity.CreatedDate = DateTime.UtcNow;
        entity.ModifiedDate = null;
        entity.CreatedById = userId;

        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.Customers.Add(entity);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create Customer", "Customer", entity.CustomerId.ToString(),
            null, Summary(entity), userId, userName, ip);
        await tx.CommitAsync();

        return (true, null, entity);
    }

    public async Task<(bool ok, string? error, Customer? customer)> UpdateAsync(
        Customer entity, string? userId, string? userName, string? ip)
    {
        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.CustomerId == entity.CustomerId);
        if (existing is null)
            return (false, "Customer not found.", null);

        Normalize(entity);

        var validationError = await ValidateAsync(entity, excludeId: existing.CustomerId);
        if (validationError is not null)
            return (false, validationError, null);

        var oldSummary = Summary(existing);

        existing.CustomerName = entity.CustomerName;
        existing.Email = entity.Email;
        existing.Phone = entity.Phone;
        existing.CompanyName = entity.CompanyName;
        existing.Address = entity.Address;
        existing.City = entity.City;
        existing.State = entity.State;
        existing.Status = entity.Status;
        existing.ModifiedDate = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync();
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update Customer", "Customer", existing.CustomerId.ToString(),
            oldSummary, Summary(existing), userId, userName, ip);
        await tx.CommitAsync();

        return (true, null, existing);
    }

    public async Task<(bool ok, string? error)> DeactivateAsync(
        int id, string? userId, string? userName, string? ip)
    {
        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.CustomerId == id);
        if (existing is null)
            return (false, "Customer not found.");
        if (existing.Status == CustomerStatus.Inactive)
            return (false, "Customer is already inactive.");

        existing.Status = CustomerStatus.Inactive;
        existing.ModifiedDate = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync();
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete Customer", "Customer", existing.CustomerId.ToString(),
            JsonSerializer.Serialize(new { existing.CustomerCode, Status = "Active" }),
            JsonSerializer.Serialize(new { existing.CustomerCode, Status = "Inactive" }),
            userId, userName, ip);
        await tx.CommitAsync();

        return (true, null);
    }

    // ---------- helpers ----------

    private static IQueryable<Customer> ApplySort(IQueryable<Customer> query, string? sortBy, string? sortDir)
    {
        var desc = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        return sortBy?.ToLowerInvariant() switch
        {
            "name" => desc
                ? query.OrderByDescending(c => c.CustomerName)
                : query.OrderBy(c => c.CustomerName),
            _ => desc
                ? query.OrderByDescending(c => c.CreatedDate)
                : query.OrderBy(c => c.CreatedDate),
        };
    }

    /// <summary>
    /// Server-side validation (independent of any client validation):
    /// required name/email/phone, email format, 10-digit Indian mobile,
    /// email unique, phone unique.
    /// </summary>
    private async Task<string?> ValidateAsync(Customer entity, int? excludeId)
    {
        if (string.IsNullOrWhiteSpace(entity.CustomerName))
            return "Customer Name is required.";
        if (string.IsNullOrWhiteSpace(entity.Email))
            return "Email is required.";
        if (!EmailFormat.IsValid(entity.Email))
            return "Enter a valid email address.";
        if (string.IsNullOrWhiteSpace(entity.Phone))
            return "Phone is required.";
        if (!PhoneFormat.IsValid(entity.Phone))
            return "Enter a valid 10-digit Indian mobile number.";

        var email = entity.Email.Trim().ToLowerInvariant();
        var phone = NormalizePhone(entity.Phone);

        if (await _db.Customers.AnyAsync(c =>
                (excludeId == null || c.CustomerId != excludeId) && c.Email.ToLower() == email))
            return "A customer with this email/phone already exists.";

        if (await _db.Customers.AnyAsync(c =>
                (excludeId == null || c.CustomerId != excludeId) && c.Phone == phone))
            return "A customer with this email/phone already exists.";

        return null;
    }

    private static void Normalize(Customer entity)
    {
        entity.CustomerName = entity.CustomerName?.Trim() ?? string.Empty;
        entity.Email = entity.Email?.Trim() ?? string.Empty;
        entity.Phone = NormalizePhone(entity.Phone);
        entity.CompanyName = string.IsNullOrWhiteSpace(entity.CompanyName) ? null : entity.CompanyName.Trim();
        entity.Address = string.IsNullOrWhiteSpace(entity.Address) ? null : entity.Address.Trim();
        entity.City = string.IsNullOrWhiteSpace(entity.City) ? null : entity.City.Trim();
        entity.State = string.IsNullOrWhiteSpace(entity.State) ? null : entity.State.Trim();
    }

    private static string NormalizePhone(string? phone)
    {
        var digits = Regex.Replace(phone ?? string.Empty, @"[\s\-+]", string.Empty);
        if (digits.StartsWith("91") && digits.Length == 12)
            digits = digits[2..];
        return digits;
    }

    private static string Summary(Customer c) => JsonSerializer.Serialize(new
    {
        c.CustomerCode,
        c.CustomerName,
        c.Email,
        c.Phone,
        c.CompanyName,
        c.City,
        c.State,
        Status = c.Status.ToString()
    });
}
