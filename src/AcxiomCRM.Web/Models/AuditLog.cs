using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Web.Models;

/// <summary>
/// Append-only audit record. No UI or API in this application may update or
/// delete audit rows — they are written only by <see cref="Services.AuditService"/>.
/// </summary>
public class AuditLog
{
    public long AuditLogId { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    [MaxLength(150)]
    public string? UserName { get; set; }

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty; // e.g. "Create Customer", "Login Success"

    [MaxLength(100)]
    public string? EntityName { get; set; }

    [MaxLength(100)]
    public string? RecordId { get; set; }

    public string? OldValue { get; set; } // JSON snapshot, truncated

    public string? NewValue { get; set; } // JSON snapshot, truncated

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(45)]
    public string? IpAddress { get; set; }
}
