using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AcxiomCRM.Tests;

/// <summary>
/// Minimal cookie jar: persists Set-Cookie values across requests so the
/// Identity auth cookie survives between API calls in a test.
/// </summary>
internal sealed class CookieJarHandler : DelegatingHandler
{
    private readonly List<string> _cookies = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_cookies.Count > 0)
            request.Headers.Add("Cookie",
                string.Join("; ", _cookies.Select(c => c.Split(';')[0].Trim())));

        var response = await base.SendAsync(request, cancellationToken);

        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            _cookies.AddRange(setCookies);

        return response;
    }
}

/// <summary>
/// WebApplicationFactory for the API integration tests. Overrides the database
/// provider to a unique SQLite file in /tmp so tests never touch the dev
/// acxiomcrm.db. One instance is shared per test class via IClassFixture.
/// </summary>
public sealed class CrmApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbFile = Path.Combine(
        Path.GetTempPath(), $"acxiom-apitest-{Guid.NewGuid():N}.db");

    public const string AdminEmail = "admin@acxiomcrm.local";
    public const string ManagerEmail = "manager@acxiomcrm.local";
    public const string SalesEmail = "sales@acxiomcrm.local";
    public const string DemoPassword = "Acxiom@123";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DatabaseProvider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_dbFile}");
    }

    /// <summary>HttpClient that persists cookies (the API uses Identity cookie auth).</summary>
    public HttpClient CreateCookieClient()
        => CreateDefaultClient(new CookieJarHandler());

    /// <summary>Logs in and returns an authenticated client with cookies persisted.</summary>
    public async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = CreateCookieClient();
        using var resp = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password,
            rememberMe = false,
        });
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Login as {email} failed with {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { File.Delete(_dbFile); }
        catch { /* best effort cleanup */ }
    }
}
