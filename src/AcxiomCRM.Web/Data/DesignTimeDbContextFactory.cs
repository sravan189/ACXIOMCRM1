using AcxiomCRM.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AcxiomCRM.Web.Data;

/// <summary>
/// Design-time factory so `dotnet ef` can build the model without a live database.
/// Defaults to the SQL Server provider (the shipped default). Override with
/// the EF_PROVIDER environment variable ("SqlServer" or "Sqlite").
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("EF_PROVIDER") ?? "SqlServer";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            options.UseSqlite("Data Source=design-time.db");
        else
            options.UseSqlServer("Server=(local);Database=AcxiomCRM_DesignTime;Trusted_Connection=True;TrustServerCertificate=True");
        return new ApplicationDbContext(options.Options);
    }
}
