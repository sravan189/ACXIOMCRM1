using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Web.ViewModels.Users;

/// <summary>
/// Admin-set temporary password for an existing user. The password itself is
/// never logged or persisted anywhere outside Identity's hash store.
/// </summary>
public class ResetPasswordViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [DataType(DataType.Password)]
    [Display(Name = "New temporary password")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "The password and confirmation password do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
