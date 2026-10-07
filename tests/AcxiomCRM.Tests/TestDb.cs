using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AcxiomCRM.Tests;

/// <summary>
/// Fresh SQLite database per test (unique file in /tmp, deleted on dispose).
/// Never touches the dev acxiomcrm.db.
/// </summary>
internal sealed class TestDb : IDisposable
{
    public ApplicationDbContext Db { get; }
    private readonly string _file;

    private TestDb(ApplicationDbContext db, string file)
    {
        Db = db;
        _file = file;
    }

    public static TestDb Create()
    {
        var file = Path.Combine(Path.GetTempPath(), $"acxiom-test-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={file}")
            .Options;
        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return new TestDb(db, file);
    }

    public void Dispose()
    {
        Db.Dispose();
        try { File.Delete(_file); }
        catch { /* best effort cleanup */ }
    }

    /// <summary>
    /// Inserts stub Identity users with the given IDs so that CreatedById /
    /// AssignedToId foreign keys are satisfied in test seeds.
    /// </summary>
    public static async Task SeedUsersAsync(ApplicationDbContext db, params string[] ids)
    {
        foreach (var id in ids.Distinct())
        {
            if (!await db.Users.AnyAsync(u => u.Id == id))
                db.Users.Add(new ApplicationUser
                {
                    Id = id,
                    UserName = id,
                    Email = $"{id}@test.local",
                    FullName = id,
                    IsActive = true,
                });
        }
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Real UserManager backed by the test database.
    /// LeadService requires a UserManager in its constructor.
    /// </summary>
    public static UserManager<ApplicationUser> NewUserManager(ApplicationDbContext db)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        return new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser>(db),
            new OptionsWrapper<IdentityOptions>(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            services,
            services.GetRequiredService<ILogger<UserManager<ApplicationUser>>>());
    }

    /// <summary>
    /// Adds an Identity user with a role directly to the test database
    /// (for AssignAsync tests — no password needed).
    /// </summary>
    public static async Task<ApplicationUser> SeedUserWithRoleAsync(
        ApplicationDbContext db, string email, string roleName)
    {
        var role = new IdentityRole(roleName)
        {
            // IdentityRole(string) leaves NormalizedName null; UserManager
            // looks roles up by normalized name, so set it explicitly.
            NormalizedName = roleName.ToUpperInvariant(),
        };
        db.Roles.Add(role);
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = email,
            IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync();
        return user;
    }
}
