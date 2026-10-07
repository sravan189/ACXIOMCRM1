using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AcxiomCRM.Web.Models;

namespace AcxiomCRM.Web.DTOs;

public sealed class FollowUpDto
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? LeadId { get; set; }
    public string? LeadName { get; set; }
    public DateTime FollowUpDate { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    public string? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime CreatedDate { get; set; }
}

public sealed class FollowUpCreateDto : IValidatableObject
{
    public int? CustomerId { get; set; }

    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Follow-up date is required.")]
    public DateTime FollowUpDate { get; set; } = DateTime.Today;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FollowUpType Type { get; set; } = FollowUpType.Call;

    [MaxLength(500)]
    public string? Remarks { get; set; }

    public string? AssignedToId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!CustomerId.HasValue && !LeadId.HasValue)
            yield return new ValidationResult(
                "Link the follow-up to a customer or a lead.",
                new[] { nameof(CustomerId), nameof(LeadId) });

        if (FollowUpDate.Date < DateTime.Today)
            yield return new ValidationResult(
                "Follow-up date cannot be in the past.",
                new[] { nameof(FollowUpDate) });
    }
}

public sealed class FollowUpUpdateDto : IValidatableObject
{
    public int? CustomerId { get; set; }

    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Follow-up date is required.")]
    public DateTime FollowUpDate { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FollowUpType Type { get; set; } = FollowUpType.Call;

    [MaxLength(500)]
    public string? Remarks { get; set; }

    public string? AssignedToId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!CustomerId.HasValue && !LeadId.HasValue)
            yield return new ValidationResult(
                "Link the follow-up to a customer or a lead.",
                new[] { nameof(CustomerId), nameof(LeadId) });

        if (FollowUpDate.Date < DateTime.Today)
            yield return new ValidationResult(
                "Follow-up date cannot be in the past.",
                new[] { nameof(FollowUpDate) });
    }
}

public sealed class FollowUpRescheduleDto : IValidatableObject
{
    [Required(ErrorMessage = "New follow-up date is required.")]
    public DateTime NewDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NewDate.Date < DateTime.Today)
            yield return new ValidationResult(
                "New follow-up date cannot be in the past.",
                new[] { nameof(NewDate) });
    }
}
