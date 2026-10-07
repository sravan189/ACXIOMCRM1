using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AcxiomCRM.Web.Models;

namespace AcxiomCRM.Web.DTOs;

public sealed class OpportunityDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? LeadId { get; set; }
    public decimal Amount { get; set; }
    public string Stage { get; set; } = string.Empty;
    public int Probability { get; set; }
    public decimal WeightedAmount { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
}

public sealed class OpportunityCreateDto : IValidatableObject
{
    [Required(ErrorMessage = "Opportunity name is required.")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public int? CustomerId { get; set; }

    public int? LeadId { get; set; }

    /// <summary>New opportunities open in Qualification, so amount must be positive.</summary>
    [Range(0.01, 9999999999.99, ErrorMessage = "Amount must be greater than zero for an open opportunity.")]
    public decimal Amount { get; set; }

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; } = 10;

    [Required(ErrorMessage = "Expected close date is required.")]
    public DateTime ExpectedCloseDate { get; set; }

    [MaxLength(100)]
    public string? Source { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public string? AssignedToId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpectedCloseDate.Date < DateTime.Today)
            yield return new ValidationResult(
                "Expected close date cannot be in the past.",
                new[] { nameof(ExpectedCloseDate) });
    }
}

/// <summary>
/// Full-update payload. Stage is intentionally absent: stage changes go through
/// POST api/opportunities/{id}/stage so transition rules always hold.
/// </summary>
public sealed class OpportunityUpdateDto : IValidatableObject
{
    [Required(ErrorMessage = "Opportunity name is required.")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public int? CustomerId { get; set; }

    public int? LeadId { get; set; }

    [Range(0, 9999999999.99, ErrorMessage = "Amount cannot be negative.")]
    public decimal Amount { get; set; }

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; }

    [Required(ErrorMessage = "Expected close date is required.")]
    public DateTime ExpectedCloseDate { get; set; }

    [MaxLength(100)]
    public string? Source { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public string? AssignedToId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpectedCloseDate.Date < DateTime.Today)
            yield return new ValidationResult(
                "Expected close date cannot be in the past for an open opportunity.",
                new[] { nameof(ExpectedCloseDate) });
    }
}

public sealed class OpportunityStageChangeDto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;
}

/// <summary>One kanban column of the sales pipeline report.</summary>
public sealed class PipelineStageDto
{
    public string Stage { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal WeightedSum { get; set; }
    public IReadOnlyList<OpportunityDto> Opportunities { get; set; } = Array.Empty<OpportunityDto>();
}

public sealed class PipelineReportDto
{
    public IReadOnlyList<PipelineStageDto> Stages { get; set; } = Array.Empty<PipelineStageDto>();
    public int TotalCount { get; set; }
    public decimal GrandTotalAmount { get; set; }
    public decimal GrandWeightedSum { get; set; }
}
