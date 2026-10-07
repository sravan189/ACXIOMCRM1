using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AcxiomCRM.Tests;

/// <summary>API auth: anonymous access, login success/failure, lockout.</summary>
public sealed class ApiAuthTests : IClassFixture<CrmApiFactory>
{
    private readonly CrmApiFactory _factory;

    public ApiAuthTests(CrmApiFactory factory) => _factory = factory;

    private static async Task<JsonDocument> JsonAsync(HttpResponseMessage resp)
        => JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

    [Fact]
    public async Task Anonymous_GetCustomers_Returns401_NotRedirect()
    {
        using var client = _factory.CreateClient(); // no cookies
        using var resp = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode); // 401, not 302
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        using var client = _factory.CreateCookieClient();
        using var resp = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = CrmApiFactory.AdminEmail,
            password = "WrongPassword1!",
            rememberMe = false,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        using var json = await JsonAsync(resp);
        Assert.Equal("invalid_credentials", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_CorrectAdminCreds_Returns200_WithAdminRole()
    {
        using var client = _factory.CreateCookieClient();
        using var resp = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = CrmApiFactory.AdminEmail,
            password = CrmApiFactory.DemoPassword,
            rememberMe = false,
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var json = await JsonAsync(resp);
        var roles = json.RootElement.GetProperty("roles")
            .EnumerateArray().Select(r => r.GetString()).ToList();
        Assert.Contains("Admin", roles);
        Assert.Equal(CrmApiFactory.AdminEmail,
            json.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Lockout_FiveFailedAttempts_ThenCorrectPassword_Still401()
    {
        // Use the MANAGER account so the admin account stays usable by other tests.
        using var client = _factory.CreateCookieClient();

        for (var i = 0; i < 5; i++)
        {
            using var bad = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = CrmApiFactory.ManagerEmail,
                password = "WrongPassword1!",
                rememberMe = false,
            });
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }

        // Correct password, but the account is now locked out -> still 401.
        using var good = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = CrmApiFactory.ManagerEmail,
            password = CrmApiFactory.DemoPassword,
            rememberMe = false,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, good.StatusCode);
        using var json = await JsonAsync(good);
        Assert.Equal("account_locked",
            json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }
}
