using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.Web.ViewModels.Activities;

/// <summary>Form model for creating/editing an activity.</summary>
public class ActivityFormViewModel
{
    public int ActivityId { get; set; }

    [Display(Name = "Activity Type")]
    public ActivityType ActivityType { get; set; } = ActivityType.Task;

    [Required(ErrorMessage = "Subject is required.")]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(1000)]
    [DataType(DataType.MultilineText)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Activity date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Activity Date")]
    public DateTime ActivityDate { get; set; } = DateTime.Today;

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [Display(Name = "Assigned To")]
    public string? AssignedToId { get; set; }

    public ActivityStatus Status { get; set; } = ActivityStatus.Planned;

    public List<SelectListItem> CustomerOptions { get; set; } = new();
    public List<SelectListItem> LeadOptions { get; set; } = new();
    public List<SelectListItem> AssigneeOptions { get; set; } = new();
}
