using System.Security.Claims;
using AcxiomCRM.Web.DTOs;
using AcxiomCRM.Web.Helpers;
using AcxiomCRM.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Web.Controllers.Api;

/// <summary>
/// Base controller for all JSON API endpoints (route prefix api/).
/// NOTE: [ApiController] is deliberately NOT used here — it would emit its own
/// 400 responses for model validation, bypassing our standard error envelope.
/// ModelState is checked manually in each action via <see cref="ValidModel"/>.
/// </summary>
[Authorize]
[IgnoreAntiforgeryToken]
[ApiExceptionFilter]
public abstract class CrmApiBaseController : ControllerBase
{
    protected string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    protected string? CurrentUserName => User.Identity?.Name;
    protected bool IsAdmin => User.IsInRole(RoleNames.Admin);
    protected bool IsManager => User.IsInRole(RoleNames.Manager);

    /// <summary>
    /// SalesExecutive sees only own/assigned records. Manager and Admin see all.
    /// </summary>
    protected bool SeesAllRecords => IsAdmin || IsManager;

    protected string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>
    /// True when ModelState is valid; otherwise produces the 400 error envelope
    /// from ModelState (set as <paramref name="result"/>).
    /// </summary>
    protected bool ValidModel(out IActionResult? result)
    {
        if (ModelState.IsValid)
        {
            result = null;
            return true;
        }

        var details = ModelState
            .Where(kv => kv.Value?.Errors.Count > 0)
            .ToDictionary(
                kv => string.IsNullOrWhiteSpace(kv.Key) ? "general" : kv.Key,
                kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray() as object);

        result = StatusCode(400, ApiErrorDto.Create(
            "validation_failed", "One or more validation errors occurred.", details));
        return false;
    }

    /// <summary>
    /// Maps tuple-style service failures (ICustomerService / ILeadService) to the
    /// error envelope: "not found" -&gt; 404, "already exists" -&gt; 409, else 400.
    /// </summary>
    protected IActionResult ServiceFailure(string error)
    {
        if (error.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return StatusCode(404, ApiErrorDto.Create("not_found", error));

        if (error.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            return StatusCode(409, ApiErrorDto.Create("duplicate", error));

        return StatusCode(400, ApiErrorDto.Create("validation_failed", error,
            new Dictionary<string, object> { ["general"] = new[] { error } }));
    }

    /// <summary>
    /// SalesExecutives may only assign records to themselves; Admins/Managers pass through.
    /// </summary>
    protected string? ResolveAssignee(string? requested)
        => SeesAllRecords ? requested : CurrentUserId;

    protected static (int page, int pageSize) Paging(int page, int pageSize)
        => (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

    protected static PagedResultDto<TDto> ToPaged<TEntity, TDto>(
        PaginatedList<TEntity> list, Func<TEntity, TDto> map) => new()
    {
        Data = list.Select(map).ToList(),
        Page = list.PageIndex,
        PageSize = list.PageSize,
        TotalCount = list.TotalCount,
        TotalPages = list.TotalPages,
    };
}
