using Microsoft.AspNetCore.Identity;

namespace MelrandiaManagement.Models;

/// <summary>Application-wide roles. Project-specific access remains in UserProjectAccess.</summary>
public static class PortalRoles
{
    public const string Administrator = "Administrator";
    public const string Member = "Member";
    public static readonly string[] All = [Administrator, Member];
}

/// <summary>Allowed roles inside one managed project.</summary>
public static class ProjectAccessRoles
{
    public const string Administrator = "Administrator";
    public const string Operator = "Operator";
    public const string Viewer = "Viewer";
    public static readonly string[] All = [Administrator, Operator, Viewer];
}

/// <summary>Central MM identity. ASP.NET Core Identity owns password hashing and lockout.</summary>
public sealed class PortalUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }
}

/// <summary>
/// Durable registry record for an independently deployed Melrandia project. URLs are operational
/// metadata only; MM never reads the child project's database.
/// </summary>
public sealed class ManagedProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProjectKey { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public string HealthUrl { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = "Planned";
    public string HealthStatus { get; set; } = "Unknown";
    public bool IsEnabled { get; set; }
    public bool IsPublic { get; set; } = true;
    public bool IsFeatured { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime? LastHealthCheckAtUtc { get; set; }
    public long? LastResponseMilliseconds { get; set; }
    public string? LastHealthError { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Explicit authorization assignment between a central user and one project.</summary>
public sealed class UserProjectAccess
{
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public string AccessRole { get; set; } = ProjectAccessRoles.Viewer;
    public bool IsEnabled { get; set; } = true;
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? GrantedByUserId { get; set; }
    public PortalUser User { get; set; } = null!;
    public ManagedProject Project { get; set; } = null!;
}

/// <summary>Append-only record of security and registry changes performed through MM.</summary>
public sealed class PortalAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Result { get; set; } = "success";
    public string? Detail { get; set; }
}

public sealed record ManagementProjectCard(
    Guid Id, string ProjectKey, string Slug, string ShortName, string DisplayName, string Description,
    string PublicUrl, string HealthUrl, string LifecycleStatus, string HealthStatus,
    bool IsEnabled, bool IsPublic, bool IsFeatured, int DisplayOrder,
    DateTime? LastHealthCheckAtUtc, long? LastResponseMilliseconds, int GrantedUserCount);

public sealed record ManagementDashboardSnapshot(
    int TotalProjects, int EnabledProjects, int HealthyProjects, int UserCount,
    IReadOnlyList<ManagementProjectCard> Projects, IReadOnlyList<PortalAuditLog> RecentActivity);

public sealed record PortalUserSummary(Guid Id, string Username, string DisplayName, bool IsEnabled,
    bool IsAdministrator, DateTime CreatedAtUtc, DateTime? LastLoginAtUtc);

public sealed record ProjectAccessSummary(Guid UserId, string Username, Guid ProjectId,
    string ProjectName, string AccessRole, bool IsEnabled, DateTime GrantedAtUtc);
