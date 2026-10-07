using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.Web.ViewModels.Users;

/// <summary>
/// Form used for both Create and Edit. <see cref="Password"/> is required
/// only on Create (enforced manually in the controller); Edit ignores it.
/// </summary>
public class UserFormViewModel
{
    public string? Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Account active")]
    public bool IsActive { get; set; } = true;

    [Required(ErrorMessage = "Please select a role.")]
    public string Role { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [Display(Name = "Temporary password")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    [Display(Name = "Confirm password")]
    public string? ConfirmPassword { get; set; }

    public List<SelectListItem> AvailableRoles { get; set; } = new();
}
