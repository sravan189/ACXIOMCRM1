using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AcxiomCRM.Web.Controllers;

/// <summary>
/// Base controller for all CRM pages. Provides the current user's id/name and
/// role helpers. All derived controllers inherit the global [Authorize] policy.
/// </summary>
public abstract class CrmBaseController : Controller
{
    protected string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    protected string? CurrentUserName => User.Identity?.Name;
    protected bool IsAdmin => User.IsInRole(Models.RoleNames.Admin);
    protected bool IsManager => User.IsInRole(Models.RoleNames.Manager);

    /// <summary>
    /// SalesExecutive sees only own/assigned records. Manager and Admin see all.
    /// </summary>
    protected bool SeesAllRecords => IsAdmin || IsManager;
}
