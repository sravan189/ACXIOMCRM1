namespace AcxiomCRM.Web.Services;

/// <summary>
/// Centralized audit logging. Implementations must be append-only:
/// nothing in the application may update or delete audit rows.
/// </summary>
public interface IAuditService
{
    Task LogAsync(
        string action,
        string? entityName = null,
        string? recordId = null,
        string? oldValue = null,
        string? newValue = null,
        string? userId = null,
        string? userName = null,
        string? ipAddress = null,
        CancellationToken ct = default);
}
