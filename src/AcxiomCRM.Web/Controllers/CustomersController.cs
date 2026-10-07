using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using AcxiomCRM.Web.ViewModels.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Web.Controllers;

[Authorize]
public class CustomersController : CrmBaseController
{
    private const int PageSize = 10;
    private readonly ICustomerService _customers;

    public CustomersController(ICustomerService customers)
    {
        _customers = customers;
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    // GET: /Customers
    public async Task<IActionResult> Index(
        string? search, CustomerStatus? status, int page = 1,
        string? sortBy = null, string? sortDir = null)
    {
        var model = await _customers.GetPagedAsync(
            search, status, Math.Max(1, page), PageSize,
            CurrentUserId, SeesAllRecords, sortBy, sortDir);

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.SortBy = string.IsNullOrWhiteSpace(sortBy) ? "created" : sortBy;
        ViewBag.SortDir = string.IsNullOrWhiteSpace(sortDir) ? "desc" : sortDir;
        return View(model);
    }

    // GET: /Customers/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var customer = await _customers.GetHistoryAsync(id, CurrentUserId, SeesAllRecords);
        if (customer is null)
            return NotFound();
        return View(customer);
    }

    // GET: /Customers/Create
    public IActionResult Create() => View(new CustomerFormViewModel());

    // POST: /Customers/Create
    [HttpPost]
    public async Task<IActionResult> Create(CustomerFormViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var (ok, error, customer) = await _customers.CreateAsync(
            MapToEntity(vm), CurrentUserId, CurrentUserName, ClientIp);

        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not create customer.");
            return View(vm);
        }

        TempData["Success"] = $"Customer {customer!.CustomerCode} created successfully.";
        return RedirectToAction(nameof(Details), new { id = customer.CustomerId });
    }

    // GET: /Customers/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _customers.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (customer is null)
            return NotFound();
        return View(MapToViewModel(customer));
    }

    // POST: /Customers/Edit/5
    [HttpPost]
    public async Task<IActionResult> Edit(int id, CustomerFormViewModel vm)
    {
        if (id != vm.CustomerId)
            return BadRequest();

        // Visibility check: invisible records get a 404 before any write is attempted.
        var visible = await _customers.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (visible is null)
            return NotFound();

        if (!ModelState.IsValid)
            return View(vm);

        var (ok, error, customer) = await _customers.UpdateAsync(
            MapToEntity(vm), CurrentUserId, CurrentUserName, ClientIp);

        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not update customer.");
            return View(vm);
        }

        TempData["Success"] = $"Customer {customer!.CustomerCode} updated successfully.";
        return RedirectToAction(nameof(Details), new { id = customer.CustomerId });
    }

    // GET: /Customers/Delete/5  (deactivate confirmation)
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _customers.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (customer is null)
            return NotFound();
        return View(customer);
    }

    // POST: /Customers/Delete/5
    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var visible = await _customers.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (visible is null)
            return NotFound();

        var (ok, error) = await _customers.DeactivateAsync(id, CurrentUserId, CurrentUserName, ClientIp);
        TempData[ok ? "Success" : "Error"] = ok
            ? $"Customer {visible.CustomerCode} deactivated."
            : error ?? "Could not deactivate customer.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- manual mapping (no AutoMapper) ----------

    private static Customer MapToEntity(CustomerFormViewModel vm) => new()
    {
        CustomerId = vm.CustomerId,
        CustomerName = vm.CustomerName,
        Email = vm.Email,
        Phone = vm.Phone,
        CompanyName = vm.CompanyName,
        Address = vm.Address,
        City = vm.City,
        State = vm.State,
        Status = vm.Status,
    };

    private static CustomerFormViewModel MapToViewModel(Customer c) => new()
    {
        CustomerId = c.CustomerId,
        CustomerName = c.CustomerName,
        Email = c.Email,
        Phone = c.Phone,
        CompanyName = c.CompanyName,
        Address = c.Address,
        City = c.City,
        State = c.State,
        Status = c.Status,
    };
}
