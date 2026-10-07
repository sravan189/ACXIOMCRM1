using AcxiomCRM.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ---- Customer ----
        builder.Entity<Customer>(e =>
        {
            e.HasIndex(c => c.CustomerCode).IsUnique();
            e.HasIndex(c => c.Email).IsUnique();
            e.HasIndex(c => c.Phone).IsUnique();
            e.HasIndex(c => c.Status);
            e.HasIndex(c => c.AssignedToId);
            e.HasOne(c => c.CreatedBy).WithMany(u => u.CreatedCustomers)
                .HasForeignKey(c => c.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.AssignedTo).WithMany()
                .HasForeignKey(c => c.AssignedToId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Lead ----
        builder.Entity<Lead>(e =>
        {
            e.HasIndex(l => l.LeadCode).IsUnique();
            e.HasIndex(l => l.Status);
            e.HasIndex(l => l.AssignedToId);
            e.HasOne(l => l.CreatedBy).WithMany()
                .HasForeignKey(l => l.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.AssignedTo).WithMany(u => u.AssignedLeads)
                .HasForeignKey(l => l.AssignedToId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Opportunity ----
        builder.Entity<Opportunity>(e =>
        {
            e.HasIndex(o => o.Stage);
            e.HasIndex(o => o.CustomerId);
            e.HasIndex(o => o.AssignedToId);
            e.HasOne(o => o.Customer).WithMany(c => c.Opportunities)
                .HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.Lead).WithMany()
                .HasForeignKey(o => o.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.CreatedBy).WithMany()
                .HasForeignKey(o => o.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.AssignedTo).WithMany(u => u.AssignedOpportunities)
                .HasForeignKey(o => o.AssignedToId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---- FollowUp ----
        builder.Entity<FollowUp>(e =>
        {
            e.HasIndex(f => f.FollowUpDate);
            e.HasIndex(f => f.Status);
            e.HasIndex(f => f.AssignedToId);
            e.HasOne(f => f.Customer).WithMany(c => c.FollowUps)
                .HasForeignKey(f => f.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Lead).WithMany(l => l.FollowUps)
                .HasForeignKey(f => f.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.AssignedTo).WithMany(u => u.AssignedFollowUps)
                .HasForeignKey(f => f.AssignedToId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.CreatedBy).WithMany()
                .HasForeignKey(f => f.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Activity ----
        builder.Entity<Activity>(e =>
        {
            e.HasIndex(a => a.ActivityDate);
            e.HasIndex(a => a.Status);
            e.HasOne(a => a.Customer).WithMany(c => c.Activities)
                .HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Lead).WithMany(l => l.Activities)
                .HasForeignKey(a => a.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.AssignedTo).WithMany(u => u.AssignedActivities)
                .HasForeignKey(a => a.AssignedToId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.CreatedBy).WithMany()
                .HasForeignKey(a => a.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });

        // ---- AuditLog: append-only, no cascade relationships ----
        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.CreatedDate);
            e.HasIndex(a => a.Action);
            e.HasIndex(a => a.EntityName);
            e.HasIndex(a => a.UserId);
            e.Property(a => a.OldValue).HasMaxLength(4000);
            e.Property(a => a.NewValue).HasMaxLength(4000);
        });
    }
}
