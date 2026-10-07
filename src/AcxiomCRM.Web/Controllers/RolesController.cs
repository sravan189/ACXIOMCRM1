using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using AcxiomCRM.Web.ViewModels.Roles;

namespace AcxiomCRM.Web.Controllers;

/// <summary>
/// Admin-only role administration. The three built-in roles (Admin, Manager,
/// SalesExecutive) and any role still assigned to users cannot be deleted.
/// </summary>
[Authorize(Roles = RoleNames.Admin)]
public class RolesController : CrmBaseController
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;

    public RolesController(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IAuditService audit)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _audit = audit;
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
    }

    // GET: /Roles
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var roles = _roleManager.Roles.OrderBy(r => r.Name).ToList();

        var items = new List<RoleListItem>();
        foreach (var role in roles)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
            items.Add(new RoleListItem
            {
                Id = role.Id,
                Name = role.Name!,
                UserCount = usersInRole.Count
            });
        }

        return View(items);
    }

    // GET: /Roles/Create
    [HttpGet]
    public IActionResult Create()
    {
        return View(new RoleFormViewModel());
    }

    // POST: /Roles/Create
    [HttpPost]
    public async Task<IActionResult> Create(RoleFormViewModel model)
    {
        model.Name = model.Name.Trim();

        if (!ModelState.IsValid)
            return View(model);

        if (await _roleManager.RoleExistsAsync(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "A role with this name already exists.");
            return View(model);
        }

        var result = await _roleManager.CreateAsync(new IdentityRole(model.Name));
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View(model);
        }

        await _audit.LogAsync("Role Created",
            entityName: "IdentityRole",
            recordId: model.Name,
            userId: CurrentUserId,
            userName: CurrentUserName,
            ipAddress: ClientIp);

        TempData["Success"] = $"Role '{model.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Roles/Delete
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null)
            return NotFound();

        if (role.Name is RoleNames.Admin or RoleNames.Manager or RoleNames.SalesExecutive)
        {
            TempData["Error"] = $"The built-in role '{role.Name}' cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
        if (usersInRole.Count > 0)
        {
            TempData["Error"] = $"Cannot delete '{role.Name}' because it is assigned to {usersInRole.Count} user(s).";
            return RedirectToAction(nameof(Index));
        }

        await _roleManager.DeleteAsync(role);

        await _audit.LogAsync("Role Deleted",
            entityName: "IdentityRole",
            recordId: role.Name,
            userId: CurrentUserId,
            userName: CurrentUserName,
            ipAddress: ClientIp);

        TempData["Success"] = $"Role '{role.Name}' deleted.";
        return RedirectToAction(nameof(Index));
    }
}
