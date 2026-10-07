using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using AcxiomCRM.Web.ViewModels.Opportunities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Web.Controllers;

[Authorize]
public class OpportunitiesController : CrmBaseController
{
    private readonly IOpportunityService _opportunities;
    private readonly ApplicationDbContext _db;

    public OpportunitiesController(IOpportunityService opportunities, ApplicationDbContext db)
    {
        _opportunities = opportunities;
        _db = db;
    }

    // ---------- List ----------

    public async Task<IActionResult> Index(
        string? search, OpportunityStage? stage, int page = 1, int pageSize = 10,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var list = await _opportunities.GetPagedAsync(
            search, stage, page, pageSize, CurrentUserId, SeesAllRecords, ct);

        ViewBag.Search = search;
        ViewBag.Stage = stage;
        return View(list);
    }

    // ---------- Kanban pipeline ----------

    public async Task<IActionResult> Pipeline(CancellationToken ct)
    {
        var stages = await _opportunities.GetPipelineAsync(CurrentUserId, SeesAllRecords, ct);
        return View(stages);
    }

    // ---------- Details ----------

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var opportunity = await _opportunities.GetByIdAsync(id, CurrentUserId, SeesAllRecords, ct);
        return opportunity is null ? NotFound() : View(opportunity);
    }

    // ---------- Create ----------

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var vm = new OpportunityFormViewModel { AssignedToId = CurrentUserId };
        await FillLookupsAsync(vm, ct);
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(OpportunityFormViewModel vm, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var created = await _opportunities.CreateAsync(
                    MapToEntity(vm), CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);
                Toast($"Opportunity '{created.OpportunityName}' created.");
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
        var o = await _opportunities.GetByIdAsync(id, CurrentUserId, SeesAllRecords, ct);
        if (o is null) return NotFound();

        var vm = new OpportunityFormViewModel
        {
            OpportunityId = o.OpportunityId,
            OpportunityName = o.OpportunityName,
            CustomerId = o.CustomerId,
            LeadId = o.LeadId,
            Amount = o.Amount,
            Stage = o.Stage,
            Probability = o.Probability,
            ExpectedCloseDate = o.ExpectedCloseDate,
            Source = o.Source,
            Notes = o.Notes,
            AssignedToId = o.AssignedToId
        };
        await FillLookupsAsync(vm, ct);
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, OpportunityFormViewModel vm, CancellationToken ct)
    {
        if (id != vm.OpportunityId) return BadRequest();

        if (ModelState.IsValid)
        {
            try
            {
                var updated = await _opportunities.UpdateAsync(
                    MapToEntity(vm), CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);
                Toast($"Opportunity '{updated.OpportunityName}' updated.");
                return RedirectToAction(nameof(Details), new { id = updated.OpportunityId });
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
        var opportunity = await _opportunities.GetByIdAsync(id, CurrentUserId, SeesAllRecords, ct);
        return opportunity is null ? NotFound() : View(opportunity);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct)
    {
        try
        {
            await _opportunities.DeleteAsync(id, CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);
            Toast("Opportunity deleted.");
        }
        catch (CrmNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }

    // ---------- Stage changes ----------

    [HttpPost]
    public async Task<IActionResult> MarkWon(int id, CancellationToken ct)
        => await ChangeStage(id, OpportunityStage.Won, ct);

    [HttpPost]
    public async Task<IActionResult> MarkLost(int id, CancellationToken ct)
        => await ChangeStage(id, OpportunityStage.Lost, ct);

    private async Task<IActionResult> ChangeStage(int id, OpportunityStage newStage, CancellationToken ct)
    {
        try
        {
            var o = await _opportunities.ChangeStageAsync(
                id, newStage, CurrentUserId, SeesAllRecords, CurrentUserName, ClientIp, ct);
            Toast($"Opportunity moved to {o.Stage}.");
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

    private static Opportunity MapToEntity(OpportunityFormViewModel vm) => new()
    {
        OpportunityId = vm.OpportunityId,
        OpportunityName = vm.OpportunityName,
        CustomerId = vm.CustomerId,
        LeadId = vm.LeadId,
        Amount = vm.Amount,
        Stage = vm.Stage,
        Probability = vm.Probability,
        ExpectedCloseDate = vm.ExpectedCloseDate,
        Source = vm.Source,
        Notes = vm.Notes,
        AssignedToId = vm.AssignedToId
    };

    private async Task FillLookupsAsync(OpportunityFormViewModel vm, CancellationToken ct)
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
