using AcxiomCRM.Web.DTOs;
using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Web.Controllers.Api;

/// <summary>
/// Read-only report endpoints that live in the API module.
/// NOTE: only the pipeline snapshot lives here; the full reports module
/// (another worker's scope) owns everything else under api/reports.
/// </summary>
[Route("api/reports")]
public sealed class ReportsController : CrmApiBaseController
{
    private readonly IOpportunityService _opportunities;

    public ReportsController(IOpportunityService opportunities)
    {
        _opportunities = opportunities;
    }

    /// <summary>Per-stage pipeline totals, counts and weighted sums.</summary>
    [HttpGet("pipeline")]
    public async Task<IActionResult> Pipeline(CancellationToken ct = default)
    {
        var stages = await _opportunities.GetPipelineAsync(CurrentUserId, SeesAllRecords, ct);
        var stageDtos = stages.Select(s => s.ToPipelineDto()).ToList();

        return Ok(new PipelineReportDto
        {
            Stages = stageDtos,
            TotalCount = stageDtos.Sum(s => s.Count),
            GrandTotalAmount = stageDtos.Sum(s => s.TotalAmount),
            GrandWeightedSum = stageDtos.Sum(s => s.WeightedSum),
        });
    }
}
