using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Validation;

namespace AcxiomCRM.Web.ViewModels.Leads;

/// <summary>
/// Form model for Lead create/edit. Status is intentionally excluded: it is
/// managed exclusively through the Change Status and Convert actions.
/// Mapped manually to/from <see cref="Lead"/>.
/// </summary>
public class LeadFormViewModel
{
    public int LeadId { get; set; }

    [Required(ErrorMessage = "Lead Name is required.")]
    [StringLength(100)]
    [Display(Name = "Lead Name")]
    public string LeadName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    public string? Email { get; set; }

    [IndianPhone(ErrorMessage = "Enter a valid 10-digit Indian mobile number.")]
    [StringLength(15)]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [StringLength(150)]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [StringLength(100)]
    public string? Source { get; set; }

    [Display(Name = "Priority")]
    public LeadPriority Priority { get; set; } = LeadPriority.Medium;

    [Range(0, 9999999999.99, ErrorMessage = "Expected Value must be 0 or greater.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Expected Value (₹)")]
    public decimal ExpectedValue { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
