using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Web.Models;

namespace AcxiomCRM.Web.Controllers;

/// <summary>Public landing + error/status-code pages. Everything else requires login.</summary>
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    /// <summary>Friendly pages for 400/401/403/404/500 (views added in hardening phase).</summary>
    [Route("/Home/StatusCode")]
    public new IActionResult StatusCode(int code)
    {
        ViewData["StatusCode"] = code;
        return View("StatusCode");
    }
}
