using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Web.Models;

public class Activity
{
    public int ActivityId { get; set; }

    public ActivityType ActivityType { get; set; } = ActivityType.Task;

    [Required(ErrorMessage = "Subject is required.")]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Activity date is required.")]
    public DateTime ActivityDate { get; set; } = DateTime.Today;

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    [MaxLength(450)]
    public string? AssignedToId { get; set; }
    public ApplicationUser? AssignedTo { get; set; }

    public ActivityStatus Status { get; set; } = ActivityStatus.Planned;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
}
