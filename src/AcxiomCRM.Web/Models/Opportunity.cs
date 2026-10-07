using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Web.Models;

public class Opportunity
{
    public int OpportunityId { get; set; }

    [Required(ErrorMessage = "Opportunity Name is required.")]
    [MaxLength(150)]
    public string OpportunityName { get; set; } = string.Empty;

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; } = 10;

    public DateTime ExpectedCloseDate { get; set; }

    [MaxLength(100)]
    public string? Source { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? ModifiedDate { get; set; }

    [MaxLength(450)]
    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    [MaxLength(450)]
    public string? AssignedToId { get; set; }
    public ApplicationUser? AssignedTo { get; set; }

    /// <summary>Derived: open unless the stage is Won/Lost.</summary>
    [NotMapped]
    public bool IsOpen => Stage != OpportunityStage.Won && Stage != OpportunityStage.Lost;

    /// <summary>Weighted Pipeline = Amount × Probability / 100.</summary>
    [NotMapped]
    public decimal WeightedAmount => Amount * Probability / 100m;
}
