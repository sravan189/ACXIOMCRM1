namespace AcxiomCRM.Web.Models;

/// <summary>Canonical role names. Users get exactly one primary role.</summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string SalesExecutive = "SalesExecutive";
}

public enum LeadStatus
{
    New = 0,
    Contacted = 1,
    Qualified = 2,
    Unqualified = 3,
    Converted = 4,
    Lost = 5
}

public enum LeadPriority
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum OpportunityStage
{
    Qualification = 0,
    Proposal = 1,
    Negotiation = 2,
    Won = 3,
    Lost = 4
}

public enum FollowUpStatus
{
    Planned = 0,
    Completed = 1,
    Missed = 2,
    Cancelled = 3
}

public enum FollowUpType
{
    Call = 0,
    Meeting = 1,
    Email = 2,
    Visit = 3
}

public enum ActivityType
{
    Call = 0,
    Meeting = 1,
    Email = 2,
    Task = 3
}

public enum ActivityStatus
{
    Planned = 0,
    Completed = 1,
    Cancelled = 2
}

public enum CustomerStatus
{
    Active = 0,
    Inactive = 1
}
