using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Validation;

namespace AcxiomCRM.Web.ViewModels.Customers;

/// <summary>Form model for Customer create/edit. Mapped manually to/from <see cref="Customer"/>.</summary>
public class CustomerFormViewModel
{
    public int CustomerId { get; set; }

    [Required(ErrorMessage = "Customer Name is required.")]
    [StringLength(100)]
    [Display(Name = "Customer Name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    [IndianPhone(ErrorMessage = "Enter a valid 10-digit Indian mobile number.")]
    [StringLength(15)]
    [Display(Name = "Phone")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [Display(Name = "Status")]
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;
}
