using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Web.Models;

/// <summary>
/// Extended Identity user. Passwords are NEVER stored here — ASP.NET Core Identity
/// manages password hashes in AspNetUsers.PasswordHash via its own secure hasher.
/// </summary>
public class ApplicationUser : IdentityUser
{
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Soft activation flag managed by admins.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Customer> CreatedCustomers { get; set; } = new List<Customer>();
    public ICollection<Lead> AssignedLeads { get; set; } = new List<Lead>();
    public ICollection<Opportunity> AssignedOpportunities { get; set; } = new List<Opportunity>();
    public ICollection<FollowUp> AssignedFollowUps { get; set; } = new List<FollowUp>();
    public ICollection<Activity> AssignedActivities { get; set; } = new List<Activity>();
}
