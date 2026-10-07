using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using AcxiomCRM.Web.ViewModels.FollowUps;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Web.Controllers;

[Authorize]
public class FollowUpsController : CrmBaseController
{
    private static readonly string[] ValidFilters = { "upcoming", "overdue", "completed" };

    private readonly IFollowUpService _followUps;
    private readonly ApplicationDbContext _db;

    public FollowUpsController(IFollowUpService followUps, ApplicationDbContext db)
    {
        _followUps = followUps;
        _db = db;
    }

    // ---------- List with tabs ----------

    public async Task<IActionResult> Index(
        string? status = "upcoming", string? search = null, string? assignedTo = null,
        int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        status = (status ?? "upcoming").Trim().ToLowerInvariant();
        if (!ValidFilters.Contains(status)) status = "upcoming";
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var list = await _followUps.GetPagedAsync(
            status, search, assignedTo, page, pageSize, CurrentUserId, SeesAllRecords, ct);

        ViewBag.Filter = status;
        ViewBag.Search = search;
        ViewBag.AssignedTo = assignedTo;
        if (SeesAllRecords)
            ViewBag.Assignees = await LookupQueries.UserOptionsAsync(_db, ct);

        return View(list);
    }

    // ---------- Details ----------

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var followUp = await _followUps.GetByIdAsync(id, CurrentUserId, SeesAllRecords, ct);
        return followUp is null ? NotFound() : View(followUp);
    }

    // ---------- Create ----------

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var vm = new FollowUpFormViewModel { AssignedToId = CurrentUserId };
        await FillLookupsAsync(vm, ct);
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(FollowUpFormViewModel vm, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var created = await _followUps.CreateAsync(
                    MapToEntity(vm), CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);
                Toast($"Follow-up scheduled for {created.FollowUpDate:dd MMM yyyy}.");
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
        var f = await _followUps.GetByIdAsync(id, CurrentUserId, SeesAllRecords, ct);
        if (f is null) return NotFound();

        var vm = new FollowUpFormViewModel
        {
            FollowUpId = f.FollowUpId,
            CustomerId = f.CustomerId,
            LeadId = f.LeadId,
            FollowUpDate = f.FollowUpDate,
            FollowUpType = f.FollowUpType,
            Remarks = f.Remarks,
            Status = f.Status,
            AssignedToId = f.AssignedToId
        };
        await FillLookupsAsync(vm, ct);
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, FollowUpFormViewModel vm, CancellationToken ct)
    {
        if (id != vm.FollowUpId) return BadRequest();

        if (ModelState.IsValid)
        {
            try
            {
                var updated = await _followUps.UpdateAsync(
                    MapToEntity(vm), CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);
                Toast("Follow-up updated.");
                return RedirectToAction(nameof(Details), new { id = updated.FollowUpId });
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
        var followUp = await _followUps.GetByIdAsync(id, CurrentUserId, SeesAllRecords, ct);
        return followUp is null ? NotFound() : View(followUp);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct)
    {
        try
        {
            await _followUps.DeleteAsync(id, CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);
            Toast("Follow-up deleted.");
        }
        catch (CrmNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }

    // ---------- Status actions ----------

    [HttpPost]
    public async Task<IActionResult> Complete(int id, CancellationToken ct)
    {
        try
        {
            await _followUps.CompleteAsync(id, CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);
            Toast("Follow-up marked as completed.");
        }
        catch (CrmNotFoundException)
        {
            return NotFound();
        }
        catch (CrmValidationException ex)
        {
            Toast(ex.Message, "danger");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Reschedule(int id, DateTime newDate, CancellationToken ct)
    {
        try
        {
            await _followUps.RescheduleAsync(id, newDate, CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);
            Toast($"Follow-up rescheduled to {newDate:dd MMM yyyy}.");
        }
        catch (CrmNotFoundException)
        {
            return NotFound();
        }
        catch (CrmValidationException ex)
        {
            Toast(ex.Message, "danger");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        try
        {
            await _followUps.CancelAsync(id, CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);
            Toast("Follow-up cancelled.");
        }
        catch (CrmNotFoundException)
        {
            return NotFound();
        }
        catch (CrmValidationException ex)
        {
            Toast(ex.Message, "danger");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // ---------- Helpers ----------

    private static FollowUp MapToEntity(FollowUpFormViewModel vm) => new()
    {
        FollowUpId = vm.FollowUpId,
        CustomerId = vm.CustomerId,
        LeadId = vm.LeadId,
        FollowUpDate = vm.FollowUpDate,
        FollowUpType = vm.FollowUpType,
        Remarks = vm.Remarks,
        Status = vm.Status,
        AssignedToId = vm.AssignedToId
    };

    private async Task FillLookupsAsync(FollowUpFormViewModel vm, CancellationToken ct)
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
