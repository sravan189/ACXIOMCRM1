using AcxiomCRM.Web.DTOs;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Web.Controllers.Api;

/// <summary>
/// Customer REST endpoints. Admins/Managers see all records; SalesExecutives see
/// only their own/assigned records (enforced by the service layer). Every mutation
/// is audited by the service layer.
/// </summary>
[Route("api/customers")]
public sealed class CustomersController : CrmApiBaseController
{
    private readonly ICustomerService _customers;

    public CustomersController(ICustomerService customers)
    {
        _customers = customers;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] CustomerStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        (page, pageSize) = Paging(page, pageSize);
        var list = await _customers.GetPagedAsync(
            search, status, page, pageSize, CurrentUserId, SeesAllRecords);
        return Ok(ToPaged(list, c => c.ToDto()));
    }

    [HttpGet("{id:int}", Name = "Api_GetCustomerById")]
    public async Task<IActionResult> GetById(int id)
    {
        var customer = await _customers.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        return customer is null
            ? StatusCode(404, ApiErrorDto.Create("not_found", $"Customer {id} not found."))
            : Ok(customer.ToDto());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomerCreateDto dto)
    {
        if (!ValidModel(out var invalid))
            return invalid!;

        var entity = new Customer
        {
            CustomerName = dto.Name,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            AssignedToId = ResolveAssignee(dto.AssignedToId),
        };

        var (ok, error, customer) = await _customers.CreateAsync(
            entity, CurrentUserId, CurrentUserName, ClientIp);

        if (!ok || customer is null)
            return ServiceFailure(error ?? "Could not create customer.");

        var created = await _customers.GetByIdAsync(customer.CustomerId, CurrentUserId, SeesAllRecords);
        return CreatedAtRoute("Api_GetCustomerById",
            new { id = customer.CustomerId }, (created ?? customer).ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CustomerUpdateDto dto)
    {
        if (!ValidModel(out var invalid))
            return invalid!;

        // Visibility guard: the service update is not scoped, so verify access first.
        var existing = await _customers.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (existing is null)
            return StatusCode(404, ApiErrorDto.Create("not_found", $"Customer {id} not found."));

        var entity = new Customer
        {
            CustomerId = id,
            CustomerName = dto.Name,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Status = existing.Status,
            AssignedToId = ResolveAssignee(dto.AssignedToId) ?? existing.AssignedToId,
        };

        var (ok, error, customer) = await _customers.UpdateAsync(
            entity, CurrentUserId, CurrentUserName, ClientIp);

        return !ok || customer is null
            ? ServiceFailure(error ?? "Could not update customer.")
            : Ok(customer.ToDto());
    }

    /// <summary>Soft delete: sets Status = Inactive.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _customers.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (existing is null)
            return StatusCode(404, ApiErrorDto.Create("not_found", $"Customer {id} not found."));

        var (ok, error) = await _customers.DeactivateAsync(
            id, CurrentUserId, CurrentUserName, ClientIp);

        return !ok
            ? ServiceFailure(error ?? "Could not deactivate customer.")
            : NoContent();
    }
}
