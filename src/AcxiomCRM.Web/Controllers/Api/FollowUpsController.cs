using AcxiomCRM.Web.DTOs;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Web.Controllers.Api;

/// <summary>
/// Follow-up REST endpoints. filter=upcoming (default) | overdue | completed.
/// The service layer throws <see cref="CrmValidationException"/> /
/// <see cref="CrmNotFoundException"/>, translated to 400 / 404 by
/// <see cref="ApiExceptionFilter"/>.
/// </summary>
[Route("api/followups")]
public sealed class FollowUpsController : CrmApiBaseController
{
    private readonly IFollowUpService _followUps;

    public FollowUpsController(IFollowUpService followUps)
    {
        _followUps = followUps;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? filter,
        [FromQuery] string? search,
        [FromQuery] string? assignedTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        (page, pageSize) = Paging(page, pageSize);
        var list = await _followUps.GetPagedAsync(
            filter, search, assignedTo, page, pageSize, CurrentUserId, SeesAllRecords, ct);
        return Ok(ToPaged(list, f => f.ToDto()));
    }

    [HttpGet("{id:int}", Name = "Api_GetFollowUpById")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        var followUp = await _followUps.GetByIdAsync(id, CurrentUserId, SeesAllRecords, ct);
        return followUp is null
            ? StatusCode(404, ApiErrorDto.Create("not_found", $"Follow-up {id} not found."))
            : Ok(followUp.ToDto());
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] FollowUpCreateDto dto, CancellationToken ct = default)
    {
        if (!ValidModel(out var invalid))
            return invalid!;

        var entity = new FollowUp
        {
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            FollowUpDate = dto.FollowUpDate,
            FollowUpType = dto.Type,
            Remarks = dto.Remarks,
            AssignedToId = ResolveAssignee(dto.AssignedToId),
        };

        var created = await _followUps.CreateAsync(
            entity, CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);

        return CreatedAtRoute("Api_GetFollowUpById",
            new { id = created.FollowUpId }, created.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id, [FromBody] FollowUpUpdateDto dto, CancellationToken ct = default)
    {
        if (!ValidModel(out var invalid))
            return invalid!;

        var entity = new FollowUp
        {
            FollowUpId = id,
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            FollowUpDate = dto.FollowUpDate,
            FollowUpType = dto.Type,
            Remarks = dto.Remarks,
            AssignedToId = ResolveAssignee(dto.AssignedToId),
        };

        var updated = await _followUps.UpdateAsync(
            entity, CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);

        return Ok(updated.ToDto());
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, CancellationToken ct = default)
    {
        var followUp = await _followUps.CompleteAsync(
            id, CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);
        return Ok(followUp.ToDto());
    }

    [HttpPost("{id:int}/reschedule")]
    public async Task<IActionResult> Reschedule(
        int id, [FromBody] FollowUpRescheduleDto dto, CancellationToken ct = default)
    {
        if (!ValidModel(out var invalid))
            return invalid!;

        var followUp = await _followUps.RescheduleAsync(
            id, dto.NewDate, CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);
        return Ok(followUp.ToDto());
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct = default)
    {
        var followUp = await _followUps.CancelAsync(
            id, CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);
        return Ok(followUp.ToDto());
    }
}
