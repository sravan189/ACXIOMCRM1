using AcxiomCRM.Web.DTOs;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AcxiomCRM.Web.Controllers.Api;

/// <summary>
/// JSON authentication endpoints. Uses the same Identity cookie as the MVC UI,
/// so API clients must persist cookies between calls.
/// POST api/auth/login is the only anonymous API endpoint.
/// </summary>
[Route("api/auth")]
public sealed class AuthController : CrmApiBaseController
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditService _audit;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAuditService audit)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _audit = audit;
    }

    /// <summary>Sign in with cookie auth. 200 + user info on success, 401 on bad credentials.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")] // brute-force protection: 10 attempts/min per IP
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        if (!ValidModel(out var invalid))
            return invalid!;

        var user = await _userManager.FindByEmailAsync(dto.Email);

        if (user is null)
        {
            await _audit.LogAsync("Login Failed", userName: dto.Email, ipAddress: ClientIp);
            return StatusCode(401, ApiErrorDto.Create("invalid_credentials", "Invalid email or password."));
        }

        if (!user.IsActive)
        {
            await _audit.LogAsync("Login Failed",
                oldValue: "Rejected: account deactivated",
                userId: user.Id, userName: user.Email, ipAddress: ClientIp);
            return StatusCode(401, ApiErrorDto.Create("account_disabled",
                "This account has been deactivated. Contact an administrator."));
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName ?? dto.Email, dto.Password, dto.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            await _audit.LogAsync("Account Locked", userId: user.Id, userName: user.Email, ipAddress: ClientIp);
            return StatusCode(401, ApiErrorDto.Create("account_locked",
                "Account is locked due to too many failed sign-in attempts. Please try again later."));
        }

        if (!result.Succeeded)
        {
            await _audit.LogAsync("Login Failed", userId: user.Id, userName: user.Email, ipAddress: ClientIp);
            return StatusCode(401, ApiErrorDto.Create("invalid_credentials", "Invalid email or password."));
        }

        await _audit.LogAsync("Login Success", userId: user.Id, userName: user.Email, ipAddress: ClientIp);

        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        return Ok(new LoginResponseDto
        {
            UserId = user.Id,
            Email = user.Email ?? dto.Email,
            FullName = user.FullName,
            Roles = roles,
            Message = "Signed in successfully.",
        });
    }

    /// <summary>Sign out (clears the auth cookie).</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _audit.LogAsync("Logout", userId: CurrentUserId, userName: CurrentUserName, ipAddress: ClientIp);
        await _signInManager.SignOutAsync();
        return Ok(new { message = "Signed out successfully." });
    }
}
