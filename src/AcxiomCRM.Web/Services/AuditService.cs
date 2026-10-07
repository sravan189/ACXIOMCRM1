using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models;

namespace AcxiomCRM.Web.Services;

/// <summary>
/// Append-only audit writer. Never logs passwords, hashes or tokens —
/// callers must not pass secrets in oldValue/newValue.
/// </summary>
public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;

    public AuditService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(
        string action,
        string? entityName = null,
        string? recordId = null,
        string? oldValue = null,
        string? newValue = null,
        string? userId = null,
        string? userName = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityName = entityName,
            RecordId = recordId,
            OldValue = Truncate(oldValue),
            NewValue = Truncate(newValue),
            UserId = userId,
            UserName = userName,
            IpAddress = ipAddress,
            CreatedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    private static string? Truncate(string? value, int max = 4000)
        => value is { Length: > 4000 } ? value[..max] : value;
}
