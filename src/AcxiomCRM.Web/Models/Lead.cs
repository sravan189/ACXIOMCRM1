using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Web.Models;

public class Lead
{
    public int LeadId { get; set; }

    [Required]
    [MaxLength(20)]
    public string LeadCode { get; set; } = string.Empty; // e.g. LEAD-0001

    [Required(ErrorMessage = "Lead Name is required.")]
    [MaxLength(100)]
    public string LeadName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(150)]
    public string? Email { get; set; }

    [MaxLength(15)]
    public string? Phone { get; set; } // validated: 10-digit Indian mobile when supplied

    [MaxLength(150)]
    public string? CompanyName { get; set; }

    [MaxLength(100)]
    public string? Source { get; set; }

    public LeadStatus Status { get; set; } = LeadStatus.New;

    public LeadPriority Priority { get; set; } = LeadPriority.Medium;

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 9999999999.99, ErrorMessage = "Expected Value must be 0 or greater.")]
    public decimal ExpectedValue { get; set; } = 0;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    [MaxLength(450)]
    public string? AssignedToId { get; set; }
    public ApplicationUser? AssignedTo { get; set; }

    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
