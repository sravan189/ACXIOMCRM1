using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Web.Controllers;

/// <summary>Dashboard landing page (default route). Thin: delegates aggregation to IDashboardService.</summary>
[Authorize]
public class DashboardController : CrmBaseController
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var vm = await _dashboard.GetDashboardAsync(CurrentUserId ?? string.Empty, SeesAllRecords, from, to);
        return View(vm);
    }
}
