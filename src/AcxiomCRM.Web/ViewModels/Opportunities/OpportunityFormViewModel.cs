using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.Web.ViewModels.Opportunities;

/// <summary>
/// Form model for creating/editing an opportunity. The conditional business
/// rules (Amount > 0 for open deals, no past close dates for open deals) live
/// in <see cref="Validate"/> and are mirrored server-side in OpportunityService,
/// so crafted requests cannot bypass them.
/// </summary>
public class OpportunityFormViewModel : IValidatableObject
{
    public int OpportunityId { get; set; }

    [Required(ErrorMessage = "Opportunity Name is required.")]
    [MaxLength(150)]
    [Display(Name = "Opportunity Name")]
    public string OpportunityName { get; set; } = string.Empty;

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [DataType(DataType.Currency)]
    [Range(typeof(decimal), "0", "9999999999999.99", ErrorMessage = "Amount cannot be negative.")]
    public decimal Amount { get; set; }

    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; } = 10;

    [Required(ErrorMessage = "Expected Close Date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Expected Close Date")]
    public DateTime ExpectedCloseDate { get; set; } = DateTime.Today.AddDays(30);

    [MaxLength(100)]
    public string? Source { get; set; }

    [MaxLength(500)]
    [DataType(DataType.MultilineText)]
    public string? Notes { get; set; }

    [Display(Name = "Assigned To")]
    public string? AssignedToId { get; set; }

    public List<SelectListItem> CustomerOptions { get; set; } = new();
    public List<SelectListItem> LeadOptions { get; set; } = new();
    public List<SelectListItem> AssigneeOptions { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var isOpen = Stage != OpportunityStage.Won && Stage != OpportunityStage.Lost;

        if (isOpen && Amount <= 0)
            yield return new ValidationResult(
                "Amount must be greater than zero for open opportunities.",
                new[] { nameof(Amount) });

        if (isOpen && ExpectedCloseDate.Date < DateTime.Today)
            yield return new ValidationResult(
                "Expected Close Date cannot be in the past.",
                new[] { nameof(ExpectedCloseDate) });
    }
}
