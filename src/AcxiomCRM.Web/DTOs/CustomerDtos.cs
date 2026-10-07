using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Web.Validation;

namespace AcxiomCRM.Web.DTOs;

public sealed class CustomerDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
}

public sealed class CustomerCreateDto
{
    [Required(ErrorMessage = "Customer name is required.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    [IndianPhone]
    [MaxLength(15)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? CompanyName { get; set; }

    [MaxLength(250)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? State { get; set; }

    /// <summary>Admins/Managers may assign to anyone; SalesExecutives are forced to themselves.</summary>
    public string? AssignedToId { get; set; }
}

/// <summary>
/// Full-update payload. Status is intentionally absent: deactivation goes through
/// DELETE api/customers/{id} (soft delete).
/// </summary>
public sealed class CustomerUpdateDto
{
    [Required(ErrorMessage = "Customer name is required.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    [IndianPhone]
    [MaxLength(15)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? CompanyName { get; set; }

    [MaxLength(250)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? State { get; set; }

    public string? AssignedToId { get; set; }
}
