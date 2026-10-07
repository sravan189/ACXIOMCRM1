using AcxiomCRM.Web.ViewModels.Dashboard;

namespace AcxiomCRM.Web.Services;

/// <summary>
/// Builds the dashboard aggregate. <paramref name="seeAll"/> mirrors
/// CrmBaseController.SeesAllRecords: Admin/Manager see everything, a
/// SalesExecutive sees only records they are assigned to or created.
/// </summary>
public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardAsync(string userId, bool seeAll, DateTime? from, DateTime? to);
}
