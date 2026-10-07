using AcxiomCRM.Web.Models;

namespace AcxiomCRM.Web.DTOs;

/// <summary>
/// Entity -&gt; DTO projections. Null-safe: navigation names are only populated
/// when the service layer included the related entity. EF entities never leak
/// past the API boundary.
/// </summary>
public static class DtoMappings
{
    public static CustomerDto ToDto(this Customer c) => new()
    {
        Id = c.CustomerId,
        Code = c.CustomerCode,
        Name = c.CustomerName,
        Email = c.Email,
        Phone = c.Phone,
        CompanyName = c.CompanyName,
        Address = c.Address,
        City = c.City,
        State = c.State,
        Status = c.Status.ToString(),
        CreatedDate = c.CreatedDate,
        ModifiedDate = c.ModifiedDate,
        AssignedToId = c.AssignedToId,
        AssignedToName = c.AssignedTo?.FullName ?? c.AssignedTo?.UserName,
    };

    public static LeadDto ToDto(this Lead l) => new()
    {
        Id = l.LeadId,
        Code = l.LeadCode,
        Name = l.LeadName,
        Email = l.Email,
        Phone = l.Phone,
        CompanyName = l.CompanyName,
        Source = l.Source,
        Status = l.Status.ToString(),
        Priority = l.Priority.ToString(),
        ExpectedValue = l.ExpectedValue,
        Notes = l.Notes,
        CreatedDate = l.CreatedDate,
        AssignedToId = l.AssignedToId,
        AssignedToName = l.AssignedTo?.FullName ?? l.AssignedTo?.UserName,
    };

    public static OpportunityDto ToDto(this Opportunity o) => new()
    {
        Id = o.OpportunityId,
        Name = o.OpportunityName,
        CustomerId = o.CustomerId,
        CustomerName = o.Customer?.CustomerName,
        LeadId = o.LeadId,
        Amount = o.Amount,
        Stage = o.Stage.ToString(),
        Probability = o.Probability,
        WeightedAmount = o.WeightedAmount,
        ExpectedCloseDate = o.ExpectedCloseDate,
        Source = o.Source,
        Notes = o.Notes,
        CreatedDate = o.CreatedDate,
        AssignedToId = o.AssignedToId,
        AssignedToName = o.AssignedTo?.FullName ?? o.AssignedTo?.UserName,
    };

    public static FollowUpDto ToDto(this FollowUp f) => new()
    {
        Id = f.FollowUpId,
        CustomerId = f.CustomerId,
        CustomerName = f.Customer?.CustomerName,
        LeadId = f.LeadId,
        LeadName = f.Lead?.LeadName,
        FollowUpDate = f.FollowUpDate,
        Type = f.FollowUpType.ToString(),
        Remarks = f.Remarks,
        Status = f.Status.ToString(),
        IsOverdue = f.IsOverdue,
        AssignedToId = f.AssignedToId,
        AssignedToName = f.AssignedTo?.FullName ?? f.AssignedTo?.UserName,
        CreatedDate = f.CreatedDate,
    };

    public static PipelineStageDto ToPipelineDto(this Services.OpportunityPipelineStage stage) => new()
    {
        Stage = stage.Stage.ToString(),
        Count = stage.Count,
        TotalAmount = stage.TotalAmount,
        WeightedSum = stage.WeightedSum,
        Opportunities = stage.Opportunities.Select(o => o.ToDto()).ToList(),
    };
}
