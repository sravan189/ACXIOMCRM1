using AcxiomCRM.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Data;

/// <summary>
/// Seeds roles, demo users and realistic sample CRM data on first run.
/// Demo credentials are documented ONLY in README.md — never hard-code
/// production secrets here.
/// </summary>
public static class DbSeeder
{
    // Demo credentials for local development (documented in README.md).
    private const string AdminEmail = "admin@acxiomcrm.local";
    private const string ManagerEmail = "manager@acxiomcrm.local";
    private const string SalesEmail = "sales@acxiomcrm.local";
    private const string DemoPassword = "Acxiom@123";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // SQL Server (shipped default) uses EF migrations. SQLite is a local
        // verification convenience: EnsureCreated builds the schema from the
        // current model since the SQL-Server-flavored migration is not
        // applicable to it.
        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
            await db.Database.EnsureCreatedAsync();
        else
            await db.Database.MigrateAsync();

        foreach (var role in new[] { RoleNames.Admin, RoleNames.Manager, RoleNames.SalesExecutive })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var admin = await EnsureUserAsync(userManager, AdminEmail, "CRM Administrator", RoleNames.Admin);
        var manager = await EnsureUserAsync(userManager, ManagerEmail, "Sales Manager", RoleNames.Manager);
        var sales = await EnsureUserAsync(userManager, SalesEmail, "Sales Executive", RoleNames.SalesExecutive);

        if (!await db.Customers.AnyAsync())
            await SeedCrmDataAsync(db, admin, manager, sales);
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager, string email, string fullName, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                IsActive = true
            };
            var result = await userManager.CreateAsync(user, DemoPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException("Seed user creation failed: " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        if (!await userManager.IsInRoleAsync(user, role))
            await userManager.AddToRoleAsync(user, role);
        return user;
    }

    private static async Task SeedCrmDataAsync(
        ApplicationDbContext db, ApplicationUser admin, ApplicationUser manager, ApplicationUser sales)
    {
        // NOTE: implemented by the auth/seed phase — realistic customers, leads,
        // opportunities, follow-ups and activities assigned across the demo users
        // so the dashboard renders meaningful KPIs and charts.
        await SeedSampleRecordsAsync(db, admin, manager, sales);
        await db.SaveChangesAsync();
    }

    private static Task SeedSampleRecordsAsync(
        ApplicationDbContext db, ApplicationUser admin, ApplicationUser manager, ApplicationUser sales)
    {
        var now = DateTime.UtcNow;

        var customers = new List<Customer>
        {
            new() { CustomerCode = "CUST-0001", CustomerName = "Aarav Sharma", Email = "aarav.sharma@techminds.in", Phone = "9812345670", CompanyName = "TechMinds Solutions", Address = "42 MG Road", City = "Bengaluru", State = "Karnataka", Status = CustomerStatus.Active, CreatedDate = now.AddDays(-120), CreatedById = admin.Id, AssignedToId = sales.Id },
            new() { CustomerCode = "CUST-0002", CustomerName = "Priya Nair", Email = "priya.nair@brightretail.com", Phone = "9812345671", CompanyName = "Bright Retail Pvt Ltd", Address = "18 Park Street", City = "Mumbai", State = "Maharashtra", Status = CustomerStatus.Active, CreatedDate = now.AddDays(-100), CreatedById = manager.Id, AssignedToId = sales.Id },
            new() { CustomerCode = "CUST-0003", CustomerName = "Rahul Verma", Email = "rahul.verma@logifast.co", Phone = "9812345672", CompanyName = "LogiFast Logistics", Address = "7 Industrial Area", City = "Delhi", State = "Delhi", Status = CustomerStatus.Active, CreatedDate = now.AddDays(-80), CreatedById = admin.Id, AssignedToId = sales.Id },
            new() { CustomerCode = "CUST-0004", CustomerName = "Sneha Iyer", Email = "sneha.iyer@healthplus.in", Phone = "9812345673", CompanyName = "HealthPlus Clinics", Address = "3 Anna Salai", City = "Chennai", State = "Tamil Nadu", Status = CustomerStatus.Active, CreatedDate = now.AddDays(-60), CreatedById = manager.Id, AssignedToId = manager.Id },
            new() { CustomerCode = "CUST-0005", CustomerName = "Vikram Rao", Email = "vikram.rao@eduspark.org", Phone = "9812345674", CompanyName = "EduSpark Learning", Address = "25 Hitech City", City = "Hyderabad", State = "Telangana", Status = CustomerStatus.Inactive, CreatedDate = now.AddDays(-40), CreatedById = admin.Id, AssignedToId = sales.Id },
            new() { CustomerCode = "CUST-0006", CustomerName = "Ananya Das", Email = "ananya.das@foodkart.in", Phone = "9812345675", CompanyName = "FoodKart", Address = "9 Salt Lake", City = "Kolkata", State = "West Bengal", Status = CustomerStatus.Active, CreatedDate = now.AddDays(-20), CreatedById = sales.Id, AssignedToId = sales.Id },
        };
        db.Customers.AddRange(customers);

        var leads = new List<Lead>
        {
            new() { LeadCode = "LEAD-0001", LeadName = "Karan Mehta", Email = "karan.mehta@startuphub.io", Phone = "9822345670", CompanyName = "StartupHub", Source = "Website", Status = LeadStatus.New, Priority = LeadPriority.High, ExpectedValue = 250000, CreatedDate = now.AddDays(-15), CreatedById = sales.Id, AssignedToId = sales.Id, Notes = "Interested in CRM demo." },
            new() { LeadCode = "LEAD-0002", LeadName = "Divya Menon", Email = "divya.menon@finserve.com", Phone = "9822345671", CompanyName = "FinServe Corp", Source = "Referral", Status = LeadStatus.Contacted, Priority = LeadPriority.Medium, ExpectedValue = 500000, CreatedDate = now.AddDays(-12), CreatedById = manager.Id, AssignedToId = sales.Id },
            new() { LeadCode = "LEAD-0003", LeadName = "Arjun Patel", Email = "arjun.patel@manutec.in", Phone = "9822345672", CompanyName = "ManuTec Industries", Source = "Cold Call", Status = LeadStatus.Qualified, Priority = LeadPriority.High, ExpectedValue = 750000, CreatedDate = now.AddDays(-10), CreatedById = sales.Id, AssignedToId = sales.Id },
            new() { LeadCode = "LEAD-0004", LeadName = "Meera Joshi", Email = "meera.joshi@travelnest.co", Phone = "9822345673", CompanyName = "TravelNest", Source = "Website", Status = LeadStatus.New, Priority = LeadPriority.Low, ExpectedValue = 120000, CreatedDate = now.AddDays(-6), CreatedById = sales.Id, AssignedToId = manager.Id },
            new() { LeadCode = "LEAD-0005", LeadName = "Sanjay Kulkarni", Email = "sanjay.k@autoparts.in", Phone = "9822345674", CompanyName = "AutoParts Ltd", Source = "Trade Show", Status = LeadStatus.Lost, Priority = LeadPriority.Medium, ExpectedValue = 300000, CreatedDate = now.AddDays(-30), CreatedById = manager.Id, AssignedToId = sales.Id, Notes = "Chose competitor." },
            new() { LeadCode = "LEAD-0006", LeadName = "Pooja Reddy", Email = "pooja.reddy@medicare.in", Phone = "9822345675", CompanyName = "MediCare Systems", Source = "Referral", Status = LeadStatus.Converted, Priority = LeadPriority.High, ExpectedValue = 900000, CreatedDate = now.AddDays(-45), CreatedById = admin.Id, AssignedToId = sales.Id },
        };
        db.Leads.AddRange(leads);

        var opportunities = new List<Opportunity>
        {
            new() { OpportunityName = "TechMinds CRM Rollout", Customer = customers[0], Amount = 1200000, Stage = OpportunityStage.Negotiation, Probability = 75, ExpectedCloseDate = DateTime.Today.AddDays(20), Source = "Existing Customer", CreatedDate = now.AddDays(-50), CreatedById = sales.Id, AssignedToId = sales.Id, Notes = "Final pricing discussion." },
            new() { OpportunityName = "Bright Retail POS Integration", Customer = customers[1], Amount = 850000, Stage = OpportunityStage.Proposal, Probability = 50, ExpectedCloseDate = DateTime.Today.AddDays(35), Source = "Existing Customer", CreatedDate = now.AddDays(-30), CreatedById = manager.Id, AssignedToId = sales.Id },
            new() { OpportunityName = "LogiFast Fleet Tracking", Customer = customers[2], Amount = 2000000, Stage = OpportunityStage.Qualification, Probability = 25, ExpectedCloseDate = DateTime.Today.AddDays(60), Source = "Existing Customer", CreatedDate = now.AddDays(-15), CreatedById = sales.Id, AssignedToId = sales.Id },
            new() { OpportunityName = "HealthPlus Appointment Suite", Customer = customers[3], Amount = 600000, Stage = OpportunityStage.Won, Probability = 100, ExpectedCloseDate = DateTime.Today.AddDays(-5), Source = "Existing Customer", CreatedDate = now.AddDays(-70), CreatedById = manager.Id, AssignedToId = manager.Id },
            new() { OpportunityName = "EduSpark LMS Deal", Customer = customers[4], Amount = 450000, Stage = OpportunityStage.Lost, Probability = 0, ExpectedCloseDate = DateTime.Today.AddDays(-10), Source = "Existing Customer", CreatedDate = now.AddDays(-90), CreatedById = admin.Id, AssignedToId = sales.Id, Notes = "Budget frozen." },
            new() { OpportunityName = "MediCare Conversion Deal", Lead = leads[5], Amount = 900000, Stage = OpportunityStage.Proposal, Probability = 60, ExpectedCloseDate = DateTime.Today.AddDays(25), Source = "Lead Conversion", CreatedDate = now.AddDays(-20), CreatedById = sales.Id, AssignedToId = sales.Id },
        };
        db.Opportunities.AddRange(opportunities);

        var followUps = new List<FollowUp>
        {
            new() { Customer = customers[0], FollowUpDate = DateTime.Today.AddDays(2), FollowUpType = FollowUpType.Call, Remarks = "Confirm pricing sign-off.", Status = FollowUpStatus.Planned, AssignedToId = sales.Id, CreatedById = sales.Id, CreatedDate = now.AddDays(-3) },
            new() { Lead = leads[1], FollowUpDate = DateTime.Today.AddDays(1), FollowUpType = FollowUpType.Email, Remarks = "Send proposal document.", Status = FollowUpStatus.Planned, AssignedToId = sales.Id, CreatedById = manager.Id, CreatedDate = now.AddDays(-2) },
            new() { Customer = customers[1], FollowUpDate = DateTime.Today.AddDays(-2), FollowUpType = FollowUpType.Meeting, Remarks = "Missed — reschedule.", Status = FollowUpStatus.Planned, AssignedToId = sales.Id, CreatedById = sales.Id, CreatedDate = now.AddDays(-9) },
            new() { Lead = leads[2], FollowUpDate = DateTime.Today.AddDays(-6), FollowUpType = FollowUpType.Call, Remarks = "Discovery call done.", Status = FollowUpStatus.Completed, AssignedToId = sales.Id, CreatedById = sales.Id, CreatedDate = now.AddDays(-10) },
            new() { Customer = customers[2], FollowUpDate = DateTime.Today.AddDays(7), FollowUpType = FollowUpType.Visit, Remarks = "Site visit for fleet demo.", Status = FollowUpStatus.Planned, AssignedToId = sales.Id, CreatedById = manager.Id, CreatedDate = now.AddDays(-1) },
        };
        db.FollowUps.AddRange(followUps);

        var activities = new List<Activity>
        {
            new() { ActivityType = ActivityType.Call, Subject = "Intro call with TechMinds", Description = "Discussed CRM requirements.", ActivityDate = DateTime.Today.AddDays(-8), Customer = customers[0], AssignedToId = sales.Id, Status = ActivityStatus.Completed, CreatedById = sales.Id },
            new() { ActivityType = ActivityType.Meeting, Subject = "Proposal walkthrough \u2014 Bright Retail", ActivityDate = DateTime.Today.AddDays(3), Customer = customers[1], AssignedToId = sales.Id, Status = ActivityStatus.Planned, CreatedById = manager.Id },
            new() { ActivityType = ActivityType.Email, Subject = "Sent quote to ManuTec", ActivityDate = DateTime.Today.AddDays(-4), Lead = leads[2], AssignedToId = sales.Id, Status = ActivityStatus.Completed, CreatedById = sales.Id },
            new() { ActivityType = ActivityType.Task, Subject = "Prepare fleet tracking demo", Description = "Demo environment setup.", ActivityDate = DateTime.Today.AddDays(5), Customer = customers[2], AssignedToId = sales.Id, Status = ActivityStatus.Planned, CreatedById = sales.Id },
        };
        db.Activities.AddRange(activities);

        return Task.CompletedTask;
    }
}
