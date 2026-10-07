using AcxiomCRM.Web.DTOs;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Web.Controllers.Api;

/// <summary>
/// Lead REST endpoints. Admins/Managers see all records; SalesExecutives see only
/// their own/assigned records (enforced by the service layer). Every mutation is
/// audited by the service layer.
/// </summary>
[Route("api/leads")]
public sealed class LeadsController : CrmApiBaseController
{
    private readonly ILeadService _leads;

    public LeadsController(ILeadService leads)
    {
        _leads = leads;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] LeadStatus? status,
        [FromQuery] string? assignedTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        (page, pageSize) = Paging(page, pageSize);
        var list = await _leads.GetPagedAsync(
            search, status, assignedTo, page, pageSize, CurrentUserId, SeesAllRecords);
        return Ok(ToPaged(list, l => l.ToDto()));
    }

    [HttpGet("{id:int}", Name = "Api_GetLeadById")]
    public async Task<IActionResult> GetById(int id)
    {
        var lead = await _leads.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        return lead is null
            ? StatusCode(404, ApiErrorDto.Create("not_found", $"Lead {id} not found."))
            : Ok(lead.ToDto());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LeadCreateDto dto)
    {
        if (!ValidModel(out var invalid))
            return invalid!;

        var entity = new Lead
        {
            LeadName = dto.Name,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Source = dto.Source,
            Priority = dto.Priority,
            ExpectedValue = dto.ExpectedValue,
            Notes = dto.Notes,
            AssignedToId = ResolveAssignee(dto.AssignedToId),
        };

        var (ok, error, lead) = await _leads.CreateAsync(
            entity, CurrentUserId, CurrentUserName, ClientIp);

        if (!ok || lead is null)
            return ServiceFailure(error ?? "Could not create lead.");

        var created = await _leads.GetByIdAsync(lead.LeadId, CurrentUserId, SeesAllRecords);
        return CreatedAtRoute("Api_GetLeadById",
            new { id = lead.LeadId }, (created ?? lead).ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] LeadUpdateDto dto)
    {
        if (!ValidModel(out var invalid))
            return invalid!;

        // Visibility guard: the service update is not scoped, so verify access first.
        var existing = await _leads.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (existing is null)
            return StatusCode(404, ApiErrorDto.Create("not_found", $"Lead {id} not found."));

        var entity = new Lead
        {
            LeadId = id,
            LeadName = dto.Name,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Source = dto.Source,
            Priority = dto.Priority,
            ExpectedValue = dto.ExpectedValue,
            Notes = dto.Notes,
            Status = existing.Status,
            AssignedToId = ResolveAssignee(dto.AssignedToId) ?? existing.AssignedToId,
        };

        var (ok, error, lead) = await _leads.UpdateAsync(
            entity, CurrentUserId, CurrentUserName, ClientIp);

        return !ok || lead is null
            ? ServiceFailure(error ?? "Could not update lead.")
            : Ok(lead.ToDto());
    }

    /// <summary>
    /// Convert a Qualified lead into a Customer (new CUST- code), optionally creating
    /// a linked Opportunity — one transaction, audited as "Lead Converted".
    /// </summary>
    [HttpPost("{id:int}/convert")]
    public async Task<IActionResult> Convert(int id, [FromBody] LeadConvertDto dto)
    {
        if (!ValidModel(out var invalid))
            return invalid!;

        var existing = await _leads.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (existing is null)
            return StatusCode(404, ApiErrorDto.Create("not_found", $"Lead {id} not found."));

        var (ok, error, customer, opportunity) = await _leads.ConvertAsync(
            id,
            dto.CreateOpportunity,
            dto.OpportunityName,
            dto.OpportunityAmount ?? 0m,
            dto.Probability ?? 10,
            dto.ExpectedCloseDate,
            CurrentUserId, CurrentUserName, ClientIp);

        if (!ok || customer is null)
            return ServiceFailure(error ?? "Could not convert lead.");

        return Ok(new
        {
            customer = customer.ToDto(),
            opportunity = opportunity?.ToDto(),
        });
    }
}
