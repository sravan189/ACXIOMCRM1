using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Web.ViewModels.Roles;

public class RoleFormViewModel
{
    [Required]
    [MaxLength(256)]
    [Display(Name = "Role name")]
    public string Name { get; set; } = string.Empty;
}
