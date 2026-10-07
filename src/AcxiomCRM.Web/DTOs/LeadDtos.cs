using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Validation;

namespace AcxiomCRM.Web.DTOs;

public sealed class LeadDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public string? Source { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal ExpectedValue { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
}

public sealed class LeadCreateDto
{
    [Required(ErrorMessage = "Lead name is required.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(150)]
    public string? Email { get; set; }

    [IndianPhone]
    [MaxLength(15)]
    public string? Phone { get; set; }

    [MaxLength(150)]
    public string? CompanyName { get; set; }

    [MaxLength(100)]
    public string? Source { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LeadPriority Priority { get; set; } = LeadPriority.Medium;

    [Range(0, 9999999999.99, ErrorMessage = "Expected value must be 0 or greater.")]
    public decimal ExpectedValue { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public string? AssignedToId { get; set; }
}

/// <summary>
/// Full-update payload. Status is intentionally absent: status changes go through
/// the lead state machine (and Converted only via POST api/leads/{id}/convert).
/// </summary>
public sealed class LeadUpdateDto
{
    [Required(ErrorMessage = "Lead name is required.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(150)]
    public string? Email { get; set; }

    [IndianPhone]
    [MaxLength(15)]
    public string? Phone { get; set; }

    [MaxLength(150)]
    public string? CompanyName { get; set; }

    [MaxLength(100)]
    public string? Source { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LeadPriority Priority { get; set; } = LeadPriority.Medium;

    [Range(0, 9999999999.99, ErrorMessage = "Expected value must be 0 or greater.")]
    public decimal ExpectedValue { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public string? AssignedToId { get; set; }
}

public sealed class LeadConvertDto : IValidatableObject
{
    /// <summary>When true, a linked opportunity is created in the same transaction.</summary>
    public bool CreateOpportunity { get; set; } = true;

    [MaxLength(150)]
    public string? OpportunityName { get; set; }

    public decimal? OpportunityAmount { get; set; }

    public int? Probability { get; set; }

    public DateTime? ExpectedCloseDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!CreateOpportunity)
            yield break;

        if (string.IsNullOrWhiteSpace(OpportunityName))
            yield return new ValidationResult(
                "Opportunity name is required when creating an opportunity.",
                new[] { nameof(OpportunityName) });

        if (!OpportunityAmount.HasValue || OpportunityAmount.Value <= 0)
            yield return new ValidationResult(
                "Opportunity amount must be greater than zero.",
                new[] { nameof(OpportunityAmount) });

        var probability = Probability ?? 10;
        if (probability < 0 || probability > 100)
            yield return new ValidationResult(
                "Probability must be between 0 and 100.",
                new[] { nameof(Probability) });

        if (!ExpectedCloseDate.HasValue)
            yield return new ValidationResult(
                "Expected close date is required when creating an opportunity.",
                new[] { nameof(ExpectedCloseDate) });
        else if (ExpectedCloseDate.Value.Date < DateTime.Today)
            yield return new ValidationResult(
                "Expected close date cannot be in the past.",
                new[] { nameof(ExpectedCloseDate) });
    }
}
