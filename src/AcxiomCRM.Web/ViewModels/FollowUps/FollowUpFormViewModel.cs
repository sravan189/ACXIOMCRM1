using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.Web.ViewModels.FollowUps;

/// <summary>
/// Form model for creating/editing a follow-up. The "no past date for
/// planned follow-ups" rule is enforced here AND server-side in FollowUpService.
/// </summary>
public class FollowUpFormViewModel : IValidatableObject
{
    public int FollowUpId { get; set; }

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Follow-up date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Follow-up Date")]
    public DateTime FollowUpDate { get; set; } = DateTime.Today;

    [Display(Name = "Follow-up Type")]
    public FollowUpType FollowUpType { get; set; } = FollowUpType.Call;

    [MaxLength(500)]
    [DataType(DataType.MultilineText)]
    public string? Remarks { get; set; }

    public FollowUpStatus Status { get; set; } = FollowUpStatus.Planned;

    [Display(Name = "Assigned To")]
    public string? AssignedToId { get; set; }

    public List<SelectListItem> CustomerOptions { get; set; } = new();
    public List<SelectListItem> LeadOptions { get; set; } = new();
    public List<SelectListItem> AssigneeOptions { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status == FollowUpStatus.Planned && FollowUpDate.Date < DateTime.Today)
            yield return new ValidationResult(
                "Follow-up date cannot be earlier than today.",
                new[] { nameof(FollowUpDate) });
    }
}
