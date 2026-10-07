namespace AcxiomCRM.Web.ViewModels.Shared;

/// <summary>Carries just enough state for the shared _Pagination partial.</summary>
public class PaginationViewModel
{
    public int PageIndex { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
    public string Action { get; set; } = "Index";
    public string? Controller { get; set; }
    public Dictionary<string, string?> RouteValues { get; set; } = new();

    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;

    /// <summary>Page numbers shown in the pager (current +/- 2, clamped).</summary>
    public IEnumerable<int> PageNumbers
    {
        get
        {
            var start = Math.Max(1, PageIndex - 2);
            var end = Math.Min(TotalPages, PageIndex + 2);
            for (var i = start; i <= end; i++) yield return i;
        }
    }
}
