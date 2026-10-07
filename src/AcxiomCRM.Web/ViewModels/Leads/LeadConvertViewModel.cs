using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Web.ViewModels.Leads;

/// <summary>Form model for converting a Qualified lead into a Customer (+ optional Opportunity).</summary>
public class LeadConvertViewModel
{
    public int LeadId { get; set; }

    [Display(Name = "Lead Code")]
    public string LeadCode { get; set; } = string.Empty;

    [Display(Name = "Lead Name")]
    public string LeadName { get; set; } = string.Empty;

    [Display(Name = "Create Opportunity")]
    public bool CreateOpportunity { get; set; } = true;

    [StringLength(150)]
    [Display(Name = "Opportunity Name")]
    public string? OpportunityName { get; set; }

    [Range(0, 9999999999.99, ErrorMessage = "Opportunity Amount must be 0 or greater.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Opportunity Amount (₹)")]
    public decimal OpportunityAmount { get; set; }

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    [Display(Name = "Probability (%)")]
    public int Probability { get; set; } = 25;

    [DataType(DataType.Date)]
    [Display(Name = "Expected Close Date")]
    public DateTime? ExpectedCloseDate { get; set; }
}
