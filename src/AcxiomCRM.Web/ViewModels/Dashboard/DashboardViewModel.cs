namespace AcxiomCRM.Web.ViewModels.Dashboard;

/// <summary>
/// Dashboard aggregate. Chart payloads are plain dictionaries/lists so they
/// serialize cleanly to JSON for Chart.js. No EF entities are exposed here.
/// </summary>
public class DashboardViewModel
{
    // Active date filter (echoed back to the filter form).
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    // KPI cards
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipelineValue { get; set; }

    // Charts (JSON-serializable for Chart.js)
    /// <summary>Lead counts keyed by LeadStatus name; all 6 statuses always present.</summary>
    public Dictionary<string, int> LeadStatusCounts { get; set; } = new();

    /// <summary>Opportunity amounts (sum of Amount) keyed by OpportunityStage name; all 5 stages always present.</summary>
    public Dictionary<string, decimal> OpportunityStageAmounts { get; set; } = new();

    /// <summary>Won opportunity totals by close month, oldest month first.</summary>
    public List<MonthlySalesPoint> MonthlySales { get; set; } = new();
}

public class MonthlySalesPoint
{
    public string Month { get; set; } = string.Empty; // e.g. "May 2026"
    public decimal Total { get; set; }
}
