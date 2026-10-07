using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using AcxiomCRM.Web.ViewModels.Activities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Web.Controllers;

[Authorize]
public class ActivitiesController : CrmBaseController
{
    private readonly IActivityService _activities;
    private readonly ApplicationDbContext _db;

    public ActivitiesController(IActivityService activities, ApplicationDbContext db)
    {
        _activities = activities;
        _db = db;
    }

    // ---------- List ----------

    public async Task<IActionResult> Index(
        string? search, ActivityType? type, ActivityStatus? status,
        int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var list = await _activities.GetPagedAsync(
            search, type, status, page, pageSize, CurrentUserId, SeesAllRecords, ct);

        ViewBag.Search = search;
        ViewBag.Type = type;
        ViewBag.Status = status;
        return View(list);
    }

    // ---------- Details ----------

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var activity = await _activities.GetByIdAsync(id, CurrentUserId, SeesAllRecords, ct);
        return activity is null ? NotFound() : View(activity);
    }

    // ---------- Create ----------

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var vm = new ActivityFormViewModel { AssignedToId = CurrentUserId };
        await FillLookupsAsync(vm, ct);
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ActivityFormViewModel vm, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var created = await _activities.CreateAsync(
                    MapToEntity(vm), CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);
                Toast($"Activity '{created.Subject}' logged.");
                return RedirectToAction(nameof(Index));
            }
            catch (CrmValidationException ex)
            {
                AddServiceErrors(ex);
            }
        }

        await FillLookupsAsync(vm, ct);
        return View(vm);
    }

    // ---------- Edit ----------

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var a = await _activities.GetByIdAsync(id, CurrentUserId, SeesAllRecords, ct);
        if (a is null) return NotFound();

        var vm = new ActivityFormViewModel
        {
            ActivityId = a.ActivityId,
            ActivityType = a.ActivityType,
            Subject = a.Subject,
            Description = a.Description,
            ActivityDate = a.ActivityDate,
            CustomerId = a.CustomerId,
            LeadId = a.LeadId,
            AssignedToId = a.AssignedToId,
            Status = a.Status
        };
        await FillLookupsAsync(vm, ct);
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, ActivityFormViewModel vm, CancellationToken ct)
    {
        if (id != vm.ActivityId) return BadRequest();

        if (ModelState.IsValid)
        {
            try
            {
                var updated = await _activities.UpdateAsync(
                    MapToEntity(vm), CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);
                Toast($"Activity '{updated.Subject}' updated.");
                return RedirectToAction(nameof(Details), new { id = updated.ActivityId });
            }
            catch (CrmNotFoundException)
            {
                return NotFound();
            }
            catch (CrmValidationException ex)
            {
                AddServiceErrors(ex);
            }
        }

        await FillLookupsAsync(vm, ct);
        return View(vm);
    }

    // ---------- Delete ----------

    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var activity = await _activities.GetByIdAsync(id, CurrentUserId, SeesAllRecords, ct);
        return activity is null ? NotFound() : View(activity);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct)
    {
        try
        {
            await _activities.DeleteAsync(id, CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);
            Toast("Activity deleted.");
        }
        catch (CrmNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }

    // ---------- Helpers ----------

    private static Activity MapToEntity(ActivityFormViewModel vm) => new()
    {
        ActivityId = vm.ActivityId,
        ActivityType = vm.ActivityType,
        Subject = vm.Subject,
        Description = vm.Description,
        ActivityDate = vm.ActivityDate,
        CustomerId = vm.CustomerId,
        LeadId = vm.LeadId,
        AssignedToId = vm.AssignedToId,
        Status = vm.Status
    };

    private async Task FillLookupsAsync(ActivityFormViewModel vm, CancellationToken ct)
    {
        vm.CustomerOptions = await LookupQueries.CustomerOptionsAsync(_db, CurrentUserId, SeesAllRecords, ct);
        vm.LeadOptions = await LookupQueries.LeadOptionsAsync(_db, CurrentUserId, SeesAllRecords, ct);
        vm.AssigneeOptions = await LookupQueries.UserOptionsAsync(_db, ct);
    }

    private void AddServiceErrors(CrmValidationException ex)
    {
        foreach (var (field, message) in ex.Errors)
            ModelState.AddModelError(field, message);
    }

    private void Toast(string message, string type = "success")
    {
        TempData[type == "danger" ? "Error" : "Success"] = message;
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}
