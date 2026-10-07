using AcxiomCRM.Web.DTOs;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Web.Controllers.Api;

/// <summary>
/// Opportunity REST endpoints. The service layer throws
/// <see cref="CrmValidationException"/> / <see cref="CrmNotFoundException"/>,
/// translated to 400 / 404 by <see cref="ApiExceptionFilter"/>.
/// </summary>
[Route("api/opportunities")]
public sealed class OpportunitiesController : CrmApiBaseController
{
    private readonly IOpportunityService _opportunities;

    public OpportunitiesController(IOpportunityService opportunities)
    {
        _opportunities = opportunities;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] OpportunityStage? stage,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        (page, pageSize) = Paging(page, pageSize);
        var list = await _opportunities.GetPagedAsync(
            search, stage, page, pageSize, CurrentUserId, SeesAllRecords, ct);
        return Ok(ToPaged(list, o => o.ToDto()));
    }

    [HttpGet("{id:int}", Name = "Api_GetOpportunityById")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        var opportunity = await _opportunities.GetByIdAsync(id, CurrentUserId, SeesAllRecords, ct);
        return opportunity is null
            ? StatusCode(404, ApiErrorDto.Create("not_found", $"Opportunity {id} not found."))
            : Ok(opportunity.ToDto());
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] OpportunityCreateDto dto, CancellationToken ct = default)
    {
        if (!ValidModel(out var invalid))
            return invalid!;

        var entity = new Opportunity
        {
            OpportunityName = dto.Name,
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            Amount = dto.Amount,
            Probability = dto.Probability,
            ExpectedCloseDate = dto.ExpectedCloseDate,
            Source = dto.Source,
            Notes = dto.Notes,
            AssignedToId = ResolveAssignee(dto.AssignedToId),
        };

        var created = await _opportunities.CreateAsync(
            entity, CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);

        return CreatedAtRoute("Api_GetOpportunityById",
            new { id = created.OpportunityId }, created.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id, [FromBody] OpportunityUpdateDto dto, CancellationToken ct = default)
    {
        if (!ValidModel(out var invalid))
            return invalid!;

        var entity = new Opportunity
        {
            OpportunityId = id,
            OpportunityName = dto.Name,
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            Amount = dto.Amount,
            Probability = dto.Probability,
            ExpectedCloseDate = dto.ExpectedCloseDate,
            Source = dto.Source,
            Notes = dto.Notes,
            AssignedToId = ResolveAssignee(dto.AssignedToId),
        };

        var updated = await _opportunities.UpdateAsync(
            entity, CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);

        return Ok(updated.ToDto());
    }

    /// <summary>Move an opportunity to a new pipeline stage (transition rules enforced).</summary>
    [HttpPost("{id:int}/stage")]
    public async Task<IActionResult> ChangeStage(
        int id, [FromBody] OpportunityStageChangeDto dto, CancellationToken ct = default)
    {
        if (!ValidModel(out var invalid))
            return invalid!;

        var updated = await _opportunities.ChangeStageAsync(
            id, dto.Stage, CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);

        return Ok(updated.ToDto());
    }
}
