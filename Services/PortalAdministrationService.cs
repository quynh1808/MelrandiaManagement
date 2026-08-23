using System.Text.Json;
using System.Text.RegularExpressions;
using MelrandiaManagement.Data;
using MelrandiaManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MelrandiaManagement.Services;

public sealed record SaveProjectRequest(Guid? Id, string ProjectKey, string Slug, string ShortName,
    string DisplayName, string Description, string PublicUrl, string HealthUrl,
    string LifecycleStatus, bool IsEnabled, bool IsPublic, bool IsFeatured, int DisplayOrder);

/// <summary>
/// Server-side boundary for MM administration. UI forms never write EF entities directly, which
/// keeps validation, role assignment and audit behavior consistent.
/// </summary>
public sealed class PortalAdministrationService(
    IDbContextFactory<ManagementDbContext> factory,
    UserManager<PortalUser> userManager,
    PublicProjectCatalog publicProjectCatalog)
{
    private static readonly Regex KeyPattern = new("^[a-z0-9](?:[a-z0-9-]{0,78}[a-z0-9])?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<ManagementDashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var projects = await ProjectCards(db).ToListAsync(cancellationToken);
        return new ManagementDashboardSnapshot(
            projects.Count,
            projects.Count(x => x.IsEnabled),
            projects.Count(x => x.HealthStatus == "Healthy"),
            await db.Users.CountAsync(cancellationToken),
            projects,
            await db.AuditLogs.AsNoTracking().OrderByDescending(x => x.TimestampUtc).Take(8)
                .ToListAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<ManagementProjectCard>> GetProjectsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await ProjectCards(db).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PortalUserSummary>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var admins = (await userManager.GetUsersInRoleAsync(PortalRoles.Administrator))
            .Select(x => x.Id).ToHashSet();
        return await userManager.Users.AsNoTracking().OrderBy(x => x.UserName)
            .Select(x => new PortalUserSummary(x.Id, x.UserName ?? string.Empty, x.DisplayName,
                x.IsEnabled, admins.Contains(x.Id), x.CreatedAtUtc, x.LastLoginAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectAccessSummary>> GetAccessAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.UserProjectAccess.AsNoTracking()
            .OrderBy(x => x.Project.DisplayOrder).ThenBy(x => x.User.UserName)
            .Select(x => new ProjectAccessSummary(x.UserId, x.User.UserName ?? string.Empty,
                x.ProjectId, x.Project.DisplayName, x.AccessRole, x.IsEnabled, x.GrantedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveProjectAsync(SaveProjectRequest request, string actor,
        CancellationToken cancellationToken = default)
    {
        ValidateProject(request);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var project = request.Id.HasValue
            ? await db.Projects.FindAsync([request.Id.Value], cancellationToken)
            : null;
        if (request.Id.HasValue && project is null)
            throw new InvalidOperationException("Dự án cần chỉnh sửa không còn tồn tại.");
        var isNew = project is null;
        project ??= new ManagedProject();
        var previous = isNew ? null : JsonSerializer.Serialize(new
        {
            project.ProjectKey,
            project.DisplayName,
            project.PublicUrl,
            project.HealthUrl,
            project.IsEnabled,
            project.LifecycleStatus
        });

        project.ProjectKey = request.ProjectKey.Trim().ToLowerInvariant();
        project.Slug = request.Slug.Trim().ToLowerInvariant();
        project.ShortName = request.ShortName.Trim().ToUpperInvariant();
        project.DisplayName = request.DisplayName.Trim();
        project.Description = request.Description.Trim();
        project.PublicUrl = request.PublicUrl.Trim();
        project.HealthUrl = request.HealthUrl.Trim();
        project.LifecycleStatus = request.LifecycleStatus.Trim();
        project.IsEnabled = request.IsEnabled;
        project.IsPublic = request.IsPublic;
        project.IsFeatured = request.IsFeatured;
        project.DisplayOrder = request.DisplayOrder;
        project.UpdatedAtUtc = DateTime.UtcNow;
        if (isNew) db.Projects.Add(project);

        db.AuditLogs.Add(Audit(actor, isNew ? "project.create" : "project.update", "project",
            project.Id.ToString(), JsonSerializer.Serialize(new { previous, request })));
        await db.SaveChangesAsync(cancellationToken);
        publicProjectCatalog.Invalidate();
    }

    public async Task<IdentityResult> CreateUserAsync(string username, string displayName,
        string password, bool administrator, string actor, CancellationToken cancellationToken = default)
    {
        username = username.Trim();
        displayName = displayName.Trim();
        if (username.Length is < 3 or > 64 || displayName.Length is < 1 or > 120)
            return IdentityResult.Failed(new IdentityError { Description = "Username hoặc tên hiển thị không hợp lệ." });

        var user = new PortalUser
        {
            Id = Guid.NewGuid(),
            UserName = username,
            DisplayName = displayName,
            IsEnabled = true,
            EmailConfirmed = true,
            LockoutEnabled = true
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded) return result;
        var role = administrator ? PortalRoles.Administrator : PortalRoles.Member;
        result = await userManager.AddToRoleAsync(user, role);
        if (!result.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return result;
        }

        await AddAuditAsync(Audit(actor, "identity.create-user", "user", user.Id.ToString(), role),
            cancellationToken);
        return IdentityResult.Success;
    }

    public async Task GrantAccessAsync(Guid userId, Guid projectId, string accessRole, Guid actorId,
        string actor, CancellationToken cancellationToken = default)
    {
        if (!ProjectAccessRoles.All.Contains(accessRole, StringComparer.Ordinal))
            throw new InvalidOperationException("Project role không hợp lệ.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (!await db.Users.AnyAsync(x => x.Id == userId && x.IsEnabled, cancellationToken) ||
            !await db.Projects.AnyAsync(x => x.Id == projectId, cancellationToken))
            throw new InvalidOperationException("User hoặc project không tồn tại/đang bị khóa.");

        var access = await db.UserProjectAccess.FindAsync([userId, projectId], cancellationToken);
        if (access is null)
        {
            access = new UserProjectAccess { UserId = userId, ProjectId = projectId };
            db.UserProjectAccess.Add(access);
        }
        access.AccessRole = accessRole;
        access.IsEnabled = true;
        access.GrantedAtUtc = DateTime.UtcNow;
        access.GrantedByUserId = actorId;
        db.AuditLogs.Add(Audit(actor, "access.grant", "user-project", $"{userId}:{projectId}", accessRole));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAccessAsync(Guid userId, Guid projectId, string actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var access = await db.UserProjectAccess.FindAsync([userId, projectId], cancellationToken);
        if (access is null) return;
        access.IsEnabled = false;
        db.AuditLogs.Add(Audit(actor, "access.revoke", "user-project", $"{userId}:{projectId}"));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordLoginAsync(PortalUser user, CancellationToken cancellationToken = default)
    {
        user.LastLoginAtUtc = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        await AddAuditAsync(Audit(user.UserName ?? user.Id.ToString(), "identity.login", "session",
            user.Id.ToString()), cancellationToken);
    }

    public Task RecordLogoutAsync(string actor, string userId, CancellationToken cancellationToken = default) =>
        AddAuditAsync(Audit(actor, "identity.logout", "session", userId), cancellationToken);

    /// <summary>
    /// Local operator recovery for an existing portal Administrator. Identity
    /// enforces the normal password policy and rotates the security stamp.
    /// </summary>
    public async Task ResetAdministratorPasswordAsync(string username, string password,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByNameAsync(username.Trim())
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản Administrator cần recovery.");
        if (!await userManager.IsInRoleAsync(user, PortalRoles.Administrator))
            throw new InvalidOperationException("Recovery chỉ chấp nhận tài khoản mang role Administrator.");

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await userManager.ResetPasswordAsync(user, token, password);
        if (!reset.Succeeded)
            throw new InvalidOperationException(
                $"Không thể reset password: {string.Join("; ", reset.Errors.Select(error => error.Description))}");

        user.IsEnabled = true;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
            throw new InvalidOperationException(
                $"Password đã đổi nhưng không thể mở khóa: {string.Join("; ", update.Errors.Select(error => error.Description))}");

        await AddAuditAsync(Audit("local-admin-recovery", "identity.recover-administrator", "user",
            user.Id.ToString(), user.UserName), cancellationToken);
    }

    private static IQueryable<ManagementProjectCard> ProjectCards(ManagementDbContext db) =>
        db.Projects.AsNoTracking().OrderBy(x => x.DisplayOrder).Select(project =>
            new ManagementProjectCard(project.Id, project.ProjectKey, project.Slug, project.ShortName,
                project.DisplayName, project.Description, project.PublicUrl, project.HealthUrl,
                project.LifecycleStatus, project.HealthStatus, project.IsEnabled, project.IsPublic,
                project.IsFeatured, project.DisplayOrder, project.LastHealthCheckAtUtc,
                project.LastResponseMilliseconds,
                db.UserProjectAccess.Count(access => access.ProjectId == project.Id && access.IsEnabled)));

    private static void ValidateProject(SaveProjectRequest request)
    {
        if (!KeyPattern.IsMatch(request.ProjectKey.Trim().ToLowerInvariant()) ||
            !KeyPattern.IsMatch(request.Slug.Trim().ToLowerInvariant()))
            throw new InvalidOperationException("ProjectKey/Slug chỉ dùng chữ thường, số và dấu gạch ngang.");
        if (string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.ShortName))
            throw new InvalidOperationException("Tên dự án và tên viết tắt là bắt buộc.");
        if (!string.IsNullOrWhiteSpace(request.PublicUrl) && !ValidUrl(request.PublicUrl))
            throw new InvalidOperationException("Public URL phải là HTTP/HTTPS URL tuyệt đối.");
        if (!string.IsNullOrWhiteSpace(request.HealthUrl) && !ValidUrl(request.HealthUrl))
            throw new InvalidOperationException("Health URL phải là HTTP/HTTPS URL tuyệt đối.");
        if (request.DisplayOrder is < 0 or > 9999)
            throw new InvalidOperationException("Thứ tự hiển thị phải trong khoảng 0-9999.");
    }

    private static bool ValidUrl(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private async Task AddAuditAsync(PortalAuditLog audit, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static PortalAuditLog Audit(string actor, string action, string entityType,
        string entityId, string? detail = null) => new()
        {
            Actor = actor,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Detail = detail
        };
}
