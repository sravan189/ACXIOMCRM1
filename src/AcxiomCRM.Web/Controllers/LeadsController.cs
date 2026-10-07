using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using AcxiomCRM.Web.ViewModels.Leads;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Web.Controllers;

[Authorize]
public class LeadsController : CrmBaseController
{
    private const int PageSize = 10;
    private readonly ILeadService _leads;
    private readonly UserManager<ApplicationUser> _userManager;

    public LeadsController(ILeadService leads, UserManager<ApplicationUser> userManager)
    {
        _leads = leads;
        _userManager = userManager;
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>Users eligible as lead assignees: active SalesExecutives and Managers.</summary>
    private async Task<List<ApplicationUser>> GetAssignableUsersAsync()
    {
        var execs = await _userManager.GetUsersInRoleAsync(RoleNames.SalesExecutive);
        var managers = await _userManager.GetUsersInRoleAsync(RoleNames.Manager);
        return execs.Concat(managers)
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .ToList();
    }

    // GET: /Leads
    public async Task<IActionResult> Index(
        string? search, LeadStatus? status, string? assignedTo, int page = 1,
        string? sortBy = null, string? sortDir = null)
    {
        var model = await _leads.GetPagedAsync(
            search, status, assignedTo, Math.Max(1, page), PageSize,
            CurrentUserId, SeesAllRecords, sortBy, sortDir);

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.AssignedTo = assignedTo;
        ViewBag.SortBy = string.IsNullOrWhiteSpace(sortBy) ? "created" : sortBy;
        ViewBag.SortDir = string.IsNullOrWhiteSpace(sortDir) ? "desc" : sortDir;
        ViewBag.ShowAssigneeFilter = SeesAllRecords;
        if (SeesAllRecords)
            ViewBag.AssigneeUsers = await GetAssignableUsersAsync();

        return View(model);
    }

    // GET: /Leads/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var lead = await _leads.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (lead is null)
            return NotFound();

        ViewBag.Opportunities = await _leads.GetOpportunitiesForLeadAsync(id, CurrentUserId, SeesAllRecords);
        return View(lead);
    }

    // GET: /Leads/Create
    public IActionResult Create() => View(new LeadFormViewModel());

    // POST: /Leads/Create
    [HttpPost]
    public async Task<IActionResult> Create(LeadFormViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var (ok, error, lead) = await _leads.CreateAsync(
            MapToEntity(vm), CurrentUserId, CurrentUserName, ClientIp);

        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not create lead.");
            return View(vm);
        }

        TempData["Success"] = $"Lead {lead!.LeadCode} created successfully.";
        return RedirectToAction(nameof(Details), new { id = lead.LeadId });
    }

    // GET: /Leads/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var lead = await _leads.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (lead is null)
            return NotFound();
        return View(MapToViewModel(lead));
    }

    // POST: /Leads/Edit/5
    [HttpPost]
    public async Task<IActionResult> Edit(int id, LeadFormViewModel vm)
    {
        if (id != vm.LeadId)
            return BadRequest();

        // Visibility check: invisible records get a 404 before any write is attempted.
        var visible = await _leads.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (visible is null)
            return NotFound();

        if (!ModelState.IsValid)
            return View(vm);

        var (ok, error, lead) = await _leads.UpdateAsync(
            MapToEntity(vm), CurrentUserId, CurrentUserName, ClientIp);

        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not update lead.");
            return View(vm);
        }

        TempData["Success"] = $"Lead {lead!.LeadCode} updated successfully.";
        return RedirectToAction(nameof(Details), new { id = lead.LeadId });
    }

    // GET: /Leads/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await _leads.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (lead is null)
            return NotFound();
        return View(lead);
    }

    // POST: /Leads/Delete/5
    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var visible = await _leads.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (visible is null)
            return NotFound();

        var (ok, error) = await _leads.DeleteAsync(id, CurrentUserId, CurrentUserName, ClientIp);
        TempData[ok ? "Success" : "Error"] = ok
            ? $"Lead {visible.LeadCode} deleted."
            : error ?? "Could not delete lead.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Leads/Assign/5
    public async Task<IActionResult> Assign(int id)
    {
        var lead = await _leads.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (lead is null)
            return NotFound();

        ViewBag.Lead = lead;
        ViewBag.Users = await GetAssignableUsersAsync();
        return View();
    }

    // POST: /Leads/Assign/5
    [HttpPost]
    public async Task<IActionResult> Assign(int id, string? assignToUserId)
    {
        var lead = await _leads.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (lead is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(assignToUserId))
        {
            TempData["Error"] = "Please select a user to assign the lead to.";
            return RedirectToAction(nameof(Assign), new { id });
        }

        var (ok, error, _) = await _leads.AssignAsync(
            id, assignToUserId, CurrentUserId, CurrentUserName, ClientIp);

        TempData[ok ? "Success" : "Error"] = ok
            ? "Lead assigned successfully."
            : error ?? "Could not assign lead.";
        return RedirectToAction(ok ? nameof(Details) : nameof(Assign), new { id });
    }

    // POST: /Leads/ChangeStatus/5
    [HttpPost]
    public async Task<IActionResult> ChangeStatus(int id, LeadStatus newStatus)
    {
        var lead = await _leads.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (lead is null)
            return NotFound();

        var (ok, error, _) = await _leads.ChangeStatusAsync(
            id, newStatus, CurrentUserId, CurrentUserName, ClientIp);

        TempData[ok ? "Success" : "Error"] = ok
            ? $"Lead status changed to {newStatus}."
            : error ?? "Could not change lead status.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET: /Leads/Convert/5
    public async Task<IActionResult> Convert(int id)
    {
        var lead = await _leads.GetByIdAsync(id, CurrentUserId, SeesAllRecords);
        if (lead is null)
            return NotFound();

        if (lead.Status != LeadStatus.Qualified)
        {
            TempData["Error"] = "Only qualified leads can be converted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var vm = new LeadConvertViewModel
        {
            LeadId = lead.LeadId,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            CreateOpportunity = true,
            OpportunityName = $"{(string.IsNullOrWhiteSpace(lead.CompanyName) ? lead.LeadName : lead.CompanyName)} — New Business",
            OpportunityAmount = lead.ExpectedValue,
            Probability = 25,
            ExpectedCloseDate = DateTime.Today.AddDays(30),
        };
        return View(vm);
    }

    // POST: /Leads/Convert/5
    [HttpPost]
    public async Task<IActionResult> Convert(LeadConvertViewModel vm)
    {
        var lead = await _leads.GetByIdAsync(vm.LeadId, CurrentUserId, SeesAllRecords);
        if (lead is null)
            return NotFound();

        if (lead.Status != LeadStatus.Qualified)
        {
            TempData["Error"] = "Only qualified leads can be converted.";
            return RedirectToAction(nameof(Details), new { id = vm.LeadId });
        }

        if (!vm.CreateOpportunity)
        {
            // Opportunity fields are disabled in the form; drop their validation state.
            ModelState.Remove(nameof(vm.OpportunityName));
            ModelState.Remove(nameof(vm.OpportunityAmount));
            ModelState.Remove(nameof(vm.Probability));
            ModelState.Remove(nameof(vm.ExpectedCloseDate));
        }

        if (!ModelState.IsValid)
            return View(vm);

        var (ok, error, customer, _) = await _leads.ConvertAsync(
            vm.LeadId, vm.CreateOpportunity, vm.OpportunityName,
            vm.OpportunityAmount, vm.Probability, vm.ExpectedCloseDate,
            CurrentUserId, CurrentUserName, ClientIp);

        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not convert lead.");
            return View(vm);
        }

        TempData["Success"] = $"Lead {lead.LeadCode} converted — customer {customer!.CustomerCode} created.";
        return RedirectToAction(nameof(Details), new { id = vm.LeadId });
    }

    // ---------- manual mapping (no AutoMapper) ----------

    private static Lead MapToEntity(LeadFormViewModel vm) => new()
    {
        LeadId = vm.LeadId,
        LeadName = vm.LeadName,
        Email = vm.Email,
        Phone = vm.Phone,
        CompanyName = vm.CompanyName,
        Source = vm.Source,
        Priority = vm.Priority,
        ExpectedValue = vm.ExpectedValue,
        Notes = vm.Notes,
    };

    private static LeadFormViewModel MapToViewModel(Lead l) => new()
    {
        LeadId = l.LeadId,
        LeadName = l.LeadName,
        Email = l.Email,
        Phone = l.Phone,
        CompanyName = l.CompanyName,
        Source = l.Source,
        Priority = l.Priority,
        ExpectedValue = l.ExpectedValue,
        Notes = l.Notes,
    };
}
