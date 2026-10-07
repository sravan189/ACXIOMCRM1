using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AcxiomCRM.Tests;

/// <summary>API customer endpoints: list shape, validation envelope, duplicates, visibility scoping.</summary>
public sealed class ApiCustomerTests : IClassFixture<CrmApiFactory>
{
    private readonly CrmApiFactory _factory;

    public ApiCustomerTests(CrmApiFactory factory) => _factory = factory;

    private static async Task<string> BodyAsync(HttpResponseMessage resp)
        => await resp.Content.ReadAsStringAsync();

    [Fact]
    public async Task Authed_GetCustomers_Returns200_WithDataArray_AndCustCodes()
    {
        using var client = await _factory.LoginAsync(CrmApiFactory.AdminEmail, CrmApiFactory.DemoPassword);
        using var resp = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await BodyAsync(resp);

        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.True(data.GetArrayLength() > 0, "expected seeded customers");

        // Spot-check: DTO shape has `code` starting with CUST-, and no EF navigation bloat.
        foreach (var item in data.EnumerateArray())
        {
            var code = item.GetProperty("code").GetString()!;
            Assert.StartsWith("CUST-", code);
        }
        Assert.DoesNotContain("\"opportunities\"", body);
        Assert.DoesNotContain("\"createdBy\"", body);
        Assert.DoesNotContain("\"followUps\"", body);
    }

    [Fact]
    public async Task PostCustomers_EmptyName_Returns400_WithErrorEnvelope()
    {
        using var client = await _factory.LoginAsync(CrmApiFactory.AdminEmail, CrmApiFactory.DemoPassword);
        using var resp = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "",
            email = "newcustomer@example.in",
            phone = "9812345678",
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        using var json = JsonDocument.Parse(await BodyAsync(resp));
        var error = json.RootElement.GetProperty("error");
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("message").GetString()));
        Assert.True(error.TryGetProperty("details", out _), "envelope must carry details");
    }

    [Fact]
    public async Task PostCustomers_DuplicateEmail_Returns409()
    {
        using var client = await _factory.LoginAsync(CrmApiFactory.AdminEmail, CrmApiFactory.DemoPassword);

        // First create succeeds...
        using var first = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "Dup Test",
            email = "dupcheck@example.in",
            phone = "9898989898",
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // ...same email again is a conflict.
        using var dup = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "Dup Test Two",
            email = "dupcheck@example.in",
            phone = "9797979797",
        });

        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        using var json = JsonDocument.Parse(await BodyAsync(dup));
        Assert.Equal("duplicate",
            json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostCustomers_Valid_Returns201_WithCustCode()
    {
        using var client = await _factory.LoginAsync(CrmApiFactory.AdminEmail, CrmApiFactory.DemoPassword);
        using var resp = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "Happy Path",
            email = "happypath@example.in",
            phone = "9696969696",
            companyName = "Happy Co",
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        using var json = JsonDocument.Parse(await BodyAsync(resp));
        Assert.StartsWith("CUST-", json.RootElement.GetProperty("code").GetString());
        Assert.NotNull(resp.Headers.Location);
    }

    [Fact]
    public async Task Sales_GetAdminOwnedCustomer_Returns404_WhileAdminGets200()
    {
        // CUST-0004 (seeded) is created by + assigned to the manager -> invisible to sales.
        using var admin = await _factory.LoginAsync(CrmApiFactory.AdminEmail, CrmApiFactory.DemoPassword);
        using var listResp = await admin.GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        using var listJson = JsonDocument.Parse(await BodyAsync(listResp));

        var id = listJson.RootElement.GetProperty("data").EnumerateArray()
            .First(i => i.GetProperty("code").GetString() == "CUST-0004")
            .GetProperty("id").GetInt32();

        using var sales = await _factory.LoginAsync(CrmApiFactory.SalesEmail, CrmApiFactory.DemoPassword);
        using var salesResp = await sales.GetAsync($"/api/customers/{id}");
        Assert.Equal(HttpStatusCode.NotFound, salesResp.StatusCode);

        using var adminResp = await admin.GetAsync($"/api/customers/{id}");
        Assert.Equal(HttpStatusCode.OK, adminResp.StatusCode);
    }
}
