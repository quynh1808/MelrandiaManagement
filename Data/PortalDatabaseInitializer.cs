using MelrandiaManagement.Configuration;
using MelrandiaManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MelrandiaManagement.Data;

/// <summary>
/// Applies schema migrations, creates central roles, seeds the AMS registry record and guarantees
/// at least one Administrator. It never changes the password of an existing account.
/// </summary>
public sealed class PortalDatabaseInitializer(
    ManagementDbContext db,
    UserManager<PortalUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<PostgreSqlOptions> databaseOptions,
    IOptions<BootstrapAdminOptions> bootstrapOptions,
    IOptions<InitialProjectOptions> projectOptions,
    ILogger<PortalDatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (databaseOptions.Value.AutoMigrate)
            await db.Database.MigrateAsync(cancellationToken);
        else if (!await db.Database.CanConnectAsync(cancellationToken))
            throw new InvalidOperationException("Không thể kết nối PostgreSQL và AutoMigrate đang tắt.");

        foreach (var role in PortalRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                EnsureSucceeded(result, $"tạo role {role}");
            }
        }

        var ams = await EnsureProjectsAsync(cancellationToken);
        await EnsureArticleCategoriesAsync(cancellationToken);
        var admin = await EnsureAdministratorAsync(cancellationToken);
        await EnsureProjectAccessAsync(admin, ams, cancellationToken);
        logger.LogInformation("PostgreSQL MM đã sẵn sàng với Project Registry và Administrator.");
    }

    private async Task EnsureArticleCategoriesAsync(CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new PortalArticleCategory { Slug = "kinh-te", DisplayName = "Kinh tế", Summary = "Góc nhìn kinh tế, thị trường và giá trị thực tiễn của các mô hình sản xuất.", DisplayOrder = 1 },
            new PortalArticleCategory { Slug = "cong-nghe", DisplayName = "Công nghệ", Summary = "Những ghi chép về Internet of Things, phần mềm, phần cứng và tự động hóa.", DisplayOrder = 2 },
            new PortalArticleCategory { Slug = "gioi-thieu-sach", DisplayName = "Giới thiệu sách", Summary = "Nơi giới thiệu những cuốn sách đáng đọc cùng các ý tưởng có thể áp dụng.", DisplayOrder = 3 }
        };
        var existing = await db.ArticleCategories.Select(x => x.Slug).ToListAsync(cancellationToken);
        foreach (var category in definitions)
            if (!existing.Contains(category.Slug, StringComparer.OrdinalIgnoreCase)) db.ArticleCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ManagedProject> EnsureProjectsAsync(CancellationToken cancellationToken)
    {
        var ams = await db.Projects.FirstOrDefaultAsync(x => x.ProjectKey == "ams", cancellationToken);
        if (ams is null)
        {
            ams = new ManagedProject
            {
                ProjectKey = "ams",
                Slug = "ams",
                ShortName = "AMS",
                DisplayName = "Aquaculture Monitoring System",
                Description = "Quản lý, giám sát cảm biến, thiết bị và hoạt động nuôi trồng thủy sản.",
                PublicUrl = projectOptions.Value.AmsPublicUrl,
                HealthUrl = projectOptions.Value.AmsHealthUrl,
                LifecycleStatus = "Active",
                IsEnabled = true,
                IsPublic = true,
                IsFeatured = true,
                DisplayOrder = 1
            };
            db.Projects.Add(ams);
        }

        if (!await db.Projects.AnyAsync(x => x.ProjectKey == "agriculture-system", cancellationToken))
        {
            db.Projects.Add(new ManagedProject
            {
                ProjectKey = "agriculture-system",
                Slug = "agriculture-system",
                ShortName = "AGS",
                DisplayName = "Agriculture System",
                Description = "Định hướng giám sát cây trồng và tự động hóa nông nghiệp thông minh.",
                LifecycleStatus = "Planned",
                IsEnabled = false,
                IsPublic = true,
                IsFeatured = true,
                DisplayOrder = 2
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return ams;
    }

    private async Task<PortalUser> EnsureAdministratorAsync(CancellationToken cancellationToken)
    {
        var currentAdmins = await userManager.GetUsersInRoleAsync(PortalRoles.Administrator);
        if (currentAdmins.Count > 0) return currentAdmins[0];

        var settings = bootstrapOptions.Value;
        var admin = await userManager.FindByNameAsync(settings.Username);
        if (admin is null)
        {
            if (string.IsNullOrWhiteSpace(settings.Password))
                throw new InvalidOperationException(
                    "Database chưa có Administrator. Hãy đặt BootstrapAdmin__Password cho lần chạy đầu.");

            admin = new PortalUser
            {
                Id = Guid.NewGuid(),
                UserName = settings.Username.Trim(),
                DisplayName = settings.DisplayName.Trim(),
                IsEnabled = true,
                EmailConfirmed = true,
                LockoutEnabled = true
            };
            EnsureSucceeded(await userManager.CreateAsync(admin, settings.Password), "tạo Administrator");
        }

        EnsureSucceeded(await userManager.AddToRoleAsync(admin, PortalRoles.Administrator),
            "gán role Administrator");
        db.AuditLogs.Add(new PortalAuditLog
        {
            Actor = "bootstrap",
            Action = "identity.bootstrap-administrator",
            EntityType = "user",
            EntityId = admin.Id.ToString(),
            Detail = admin.UserName
        });
        await db.SaveChangesAsync(cancellationToken);
        return admin;
    }

    private async Task EnsureProjectAccessAsync(PortalUser admin, ManagedProject ams,
        CancellationToken cancellationToken)
    {
        if (await db.UserProjectAccess.AnyAsync(x => x.UserId == admin.Id && x.ProjectId == ams.Id,
                cancellationToken)) return;

        db.UserProjectAccess.Add(new UserProjectAccess
        {
            UserId = admin.Id,
            ProjectId = ams.Id,
            AccessRole = ProjectAccessRoles.Administrator,
            GrantedByUserId = admin.Id
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded) return;
        throw new InvalidOperationException(
            $"Không thể {operation}: {string.Join("; ", result.Errors.Select(x => x.Description))}");
    }
}
