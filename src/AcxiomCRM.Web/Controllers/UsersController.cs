using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Web.Helpers;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using AcxiomCRM.Web.ViewModels.Users;

namespace AcxiomCRM.Web.Controllers;

/// <summary>
/// Admin-only user administration: list/search, create, edit, activate,
/// password reset and account unlock. Password hashes are never exposed.
/// </summary>
[Authorize(Roles = RoleNames.Admin)]
public class UsersController : CrmBaseController
{
    private const int PageSize = 10;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditService _audit;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditService audit)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _audit = audit;
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
    }

    private async Task PopulateRolesAsync(UserFormViewModel model)
    {
        var roles = _roleManager.Roles.OrderBy(r => r.Name).ToList();
        model.AvailableRoles = roles
            .Select(r => new SelectListItem(r.Name, r.Name, string.Equals(r.Name, model.Role, StringComparison.Ordinal)))
            .ToList();
        await Task.CompletedTask;
    }

    // GET: /Users
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => u.FullName.Contains(term) || u.Email!.Contains(term));
        }

        query = query.OrderBy(u => u.FullName);

        var paged = await PaginatedList<ApplicationUser>.CreateAsync(query, page, PageSize);

        var items = new List<UserListItem>();
        foreach (var u in paged)
        {
            var roles = await _userManager.GetRolesAsync(u);
            items.Add(new UserListItem
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive,
                Roles = roles.ToList(),
                IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow,
                LockoutEnd = u.LockoutEnd,
                CreatedDate = u.CreatedDate
            });
        }

        ViewData["Search"] = search;
        return View(new PaginatedList<UserListItem>(items, paged.TotalCount, paged.PageIndex, paged.PageSize));
    }

    // GET: /Users/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new UserFormViewModel();
        await PopulateRolesAsync(model);
        return View(model);
    }

    // POST: /Users/Create
    [HttpPost]
    public async Task<IActionResult> Create(UserFormViewModel model)
    {
        await PopulateRolesAsync(model);

        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "A password is required.");

        if (!ModelState.IsValid)
            return View(model);

        if (await _roleManager.FindByNameAsync(model.Role) is null)
        {
            ModelState.AddModelError(nameof(model.Role), "The selected role does not exist.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            FullName = model.FullName,
            Email = model.Email,
            UserName = model.Email,
            PhoneNumber = model.PhoneNumber,
            EmailConfirmed = true,
            IsActive = model.IsActive,
            CreatedDate = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password!);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, model.Role);

        await _audit.LogAsync("User Created",
            entityName: "ApplicationUser",
            recordId: user.Id,
            newValue: $"Role: {model.Role}",
            userId: CurrentUserId,
            userName: CurrentUserName,
            ipAddress: ClientIp);

        TempData["Success"] = $"User '{user.Email}' created.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Users/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);

        var model = new UserFormViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            Role = roles.FirstOrDefault() ?? string.Empty
        };
        await PopulateRolesAsync(model);
        return View(model);
    }

    // POST: /Users/Edit
    [HttpPost]
    public async Task<IActionResult> Edit(UserFormViewModel model)
    {
        await PopulateRolesAsync(model);

        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByIdAsync(model.Id ?? string.Empty);
        if (user is null)
            return NotFound();

        if (await _roleManager.FindByNameAsync(model.Role) is null)
        {
            ModelState.AddModelError(nameof(model.Role), "The selected role does not exist.");
            return View(model);
        }

        user.FullName = model.FullName;
        user.Email = model.Email;
        user.UserName = model.Email;
        user.PhoneNumber = model.PhoneNumber;
        user.IsActive = model.IsActive;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            AddErrors(updateResult);
            return View(model);
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(model.Role, StringComparer.Ordinal))
        {
            var oldRoles = string.Join(", ", currentRoles);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, model.Role);

            await _audit.LogAsync("Role Changed",
                entityName: "ApplicationUser",
                recordId: user.Id,
                oldValue: oldRoles,
                newValue: model.Role,
                userId: CurrentUserId,
                userName: CurrentUserName,
                ipAddress: ClientIp);

            TempData["Success"] = $"User updated. Role changed from '{oldRoles}' to '{model.Role}'.";
        }
        else
        {
            await _audit.LogAsync("User Updated",
                entityName: "ApplicationUser",
                recordId: user.Id,
                userId: CurrentUserId,
                userName: CurrentUserName,
                ipAddress: ClientIp);

            TempData["Success"] = "User updated.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /Users/ToggleActive
    [HttpPost]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        if (string.Equals(user.Id, CurrentUserId, StringComparison.Ordinal))
        {
            TempData["Error"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = !user.IsActive;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = "Could not update the user's status.";
            return RedirectToAction(nameof(Index));
        }

        await _audit.LogAsync(
            user.IsActive ? "User Activated" : "User Deactivated",
            entityName: "ApplicationUser",
            recordId: user.Id,
            userId: CurrentUserId,
            userName: CurrentUserName,
            ipAddress: ClientIp);

        TempData["Success"] = user.IsActive
            ? $"User '{user.Email}' activated."
            : $"User '{user.Email}' deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Users/ResetPassword — the new password is never logged.
    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user is null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                TempData["Error"] = error.ErrorMessage;
            return RedirectToAction(nameof(Edit), new { id = model.UserId });
        }

        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
        {
            TempData["Error"] = "Could not reset the password.";
            return RedirectToAction(nameof(Edit), new { id = model.UserId });
        }

        var addResult = await _userManager.AddPasswordAsync(user, model.NewPassword);
        if (!addResult.Succeeded)
        {
            TempData["Error"] = addResult.Errors.FirstOrDefault()?.Description
                ?? "Could not set the new password.";
            return RedirectToAction(nameof(Edit), new { id = model.UserId });
        }

        await _audit.LogAsync("Password Reset",
            entityName: "ApplicationUser",
            recordId: user.Id,
            userId: CurrentUserId,
            userName: CurrentUserName,
            ipAddress: ClientIp);

        TempData["Success"] = $"Password reset for '{user.Email}'. Share the new temporary password securely.";
        return RedirectToAction(nameof(Edit), new { id = model.UserId });
    }

    // POST: /Users/Unlock
    [HttpPost]
    public async Task<IActionResult> Unlock(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        await _userManager.SetLockoutEndDateAsync(user, null);

        await _audit.LogAsync("Account Unlocked",
            entityName: "ApplicationUser",
            recordId: user.Id,
            userId: CurrentUserId,
            userName: CurrentUserName,
            ipAddress: ClientIp);

        TempData["Success"] = $"Account '{user.Email}' unlocked.";
        return RedirectToAction(nameof(Index));
    }
}
