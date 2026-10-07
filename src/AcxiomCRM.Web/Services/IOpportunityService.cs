using AcxiomCRM.Web.Helpers;
using AcxiomCRM.Web.Models;

namespace AcxiomCRM.Web.Services;

public interface IOpportunityService
{
    Task<PaginatedList<Opportunity>> GetPagedAsync(
        string? search, OpportunityStage? stage, int page, int pageSize,
        string? userId, bool seeAll, CancellationToken ct = default);

    Task<Opportunity?> GetByIdAsync(int id, string? userId, bool seeAll, CancellationToken ct = default);

    Task<Opportunity> CreateAsync(
        Opportunity opportunity, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default);

    Task<Opportunity> UpdateAsync(
        Opportunity opportunity, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default);

    Task DeleteAsync(
        int id, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default);

    Task<Opportunity> ChangeStageAsync(
        int id, OpportunityStage newStage, string? userId, bool seeAll, string? userName,
        string? ipAddress = null, CancellationToken ct = default);

    Task<IReadOnlyList<OpportunityPipelineStage>> GetPipelineAsync(
        string? userId, bool seeAll, CancellationToken ct = default);
}

/// <summary>One kanban column of the sales pipeline.</summary>
public sealed class OpportunityPipelineStage
{
    public OpportunityStage Stage { get; init; }
    public List<Opportunity> Opportunities { get; init; } = new();
    public int Count => Opportunities.Count;
    public decimal TotalAmount => Opportunities.Sum(o => o.Amount);
    public decimal WeightedSum => Opportunities.Sum(o => o.WeightedAmount);
}
