using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AcxiomCRM.Tests;

/// <summary>API lead conversion guard and the pipeline report endpoint.</summary>
public sealed class ApiLeadConvertAndPipelineTests : IClassFixture<CrmApiFactory>
{
    private readonly CrmApiFactory _factory;

    public ApiLeadConvertAndPipelineTests(CrmApiFactory factory) => _factory = factory;

    private static async Task<int> LeadIdByCodeAsync(HttpClient client, string code)
    {
        using var resp = await client.GetAsync("/api/leads?page=1&pageSize=50");
        resp.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").EnumerateArray()
            .First(i => i.GetProperty("code").GetString() == code)
            .GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task PostLeads_ConvertNewLead_Returns400_MustBeQualified()
    {
        using var client = await _factory.LoginAsync(CrmApiFactory.AdminEmail, CrmApiFactory.DemoPassword);
        var leadId = await LeadIdByCodeAsync(client, "LEAD-0001"); // seeded as New

        using var resp = await client.PostAsJsonAsync($"/api/leads/{leadId}/convert", new
        {
            createOpportunity = false,
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var error = json.RootElement.GetProperty("error");
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.Contains("qualified", error.GetProperty("message").GetString()!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetPipeline_Returns200_WithAllFiveStages()
    {
        using var client = await _factory.LoginAsync(CrmApiFactory.AdminEmail, CrmApiFactory.DemoPassword);
        using var resp = await client.GetAsync("/api/reports/pipeline");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

        var stages = json.RootElement.GetProperty("stages").EnumerateArray().ToList();
        Assert.Equal(5, stages.Count);

        var names = stages.Select(s => s.GetProperty("stage").GetString()!).ToHashSet();
        Assert.Equal(
            new HashSet<string> { "Qualification", "Proposal", "Negotiation", "Won", "Lost" },
            names);

        // Seeded data: 6 opportunities in total.
        Assert.Equal(6, json.RootElement.GetProperty("totalCount").GetInt32());
    }
}
