using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using AcxiomCRM.Web.ViewModels.Account;

namespace AcxiomCRM.Web.Controllers;

/// <summary>
/// Sign-in, self-registration and sign-out. Login/Register/AccessDenied are
/// anonymous; everything else inherits the global [Authorize] policy.
/// </summary>
public class AccountController : CrmBaseController
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditService _audit;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAuditService audit)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _audit = audit;
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("login")] // brute-force protection: 10 attempts/min per IP
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user is null)
        {
            await _audit.LogAsync("Login Failed", userName: model.Email, ipAddress: ClientIp);
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        if (!user.IsActive)
        {
            await _audit.LogAsync("Login Failed",
                oldValue: "Rejected: account deactivated",
                userId: user.Id, userName: user.Email, ipAddress: ClientIp);
            ModelState.AddModelError(string.Empty, "Account is deactivated.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName ?? model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _audit.LogAsync("Login Success", userId: user.Id, userName: user.Email, ipAddress: ClientIp);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        if (result.IsLockedOut)
        {
            await _audit.LogAsync("Account Locked", userId: user.Id, userName: user.Email, ipAddress: ClientIp);
            ModelState.AddModelError(string.Empty,
                "Account is locked due to too many failed sign-in attempts. Please try again later.");
        }
        else
        {
            await _audit.LogAsync("Login Failed", userId: user.Id, userName: user.Email, ipAddress: ClientIp);
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
        }

        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = new ApplicationUser
        {
            FullName = model.FullName,
            Email = model.Email,
            UserName = model.Email,
            PhoneNumber = model.Phone,
            EmailConfirmed = true,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            AddErrors(result);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, RoleNames.SalesExecutive);
        await _signInManager.SignInAsync(user, isPersistent: false);

        await _audit.LogAsync("User Registered",
            entityName: "ApplicationUser",
            recordId: user.Id,
            newValue: $"Role: {RoleNames.SalesExecutive}",
            userId: user.Id,
            userName: user.Email,
            ipAddress: ClientIp);

        TempData["Success"] = "Welcome to AcxiomCRM! Your account has been created.";
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _audit.LogAsync("Logout", userId: CurrentUserId, userName: CurrentUserName, ipAddress: ClientIp);
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
