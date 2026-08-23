using MelrandiaManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MelrandiaManagement.Data;

/// <summary>
/// PostgreSQL unit of work for central identity, project registry, access grants and audit.
/// Child-project telemetry and operational data deliberately stay outside this database.
/// </summary>
public sealed class ManagementDbContext(DbContextOptions<ManagementDbContext> options)
    : IdentityDbContext<PortalUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<ManagedProject> Projects => Set<ManagedProject>();
    public DbSet<UserProjectAccess> UserProjectAccess => Set<UserProjectAccess>();
    public DbSet<PortalAuditLog> AuditLogs => Set<PortalAuditLog>();
    public DbSet<PortalArticleCategory> ArticleCategories => Set<PortalArticleCategory>();
    public DbSet<PortalArticle> Articles => Set<PortalArticle>();
    public DbSet<PortalArticleMedia> ArticleMedia => Set<PortalArticleMedia>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PortalUser>().ToTable("users");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        builder.Entity<PortalUser>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(120);
            entity.HasIndex(x => x.IsEnabled);
        });

        builder.Entity<ManagedProject>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProjectKey).HasMaxLength(40);
            entity.Property(x => x.Slug).HasMaxLength(80);
            entity.Property(x => x.ShortName).HasMaxLength(20);
            entity.Property(x => x.DisplayName).HasMaxLength(160);
            entity.Property(x => x.PublicUrl).HasMaxLength(500);
            entity.Property(x => x.HealthUrl).HasMaxLength(500);
            entity.HasIndex(x => x.ProjectKey).IsUnique();
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => new { x.IsPublic, x.DisplayOrder });
        });

        builder.Entity<UserProjectAccess>(entity =>
        {
            entity.ToTable("user_project_access");
            entity.HasKey(x => new { x.UserId, x.ProjectId });
            entity.Property(x => x.AccessRole).HasMaxLength(40);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PortalAuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TimestampUtc);
            entity.Property(x => x.Actor).HasMaxLength(120);
            entity.Property(x => x.Action).HasMaxLength(120);
            entity.Property(x => x.EntityType).HasMaxLength(80);
            entity.Property(x => x.EntityId).HasMaxLength(160);
            entity.Property(x => x.Result).HasMaxLength(40);
        });

        builder.Entity<PortalArticleCategory>(entity =>
        {
            entity.ToTable("article_categories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Slug).HasMaxLength(80);
            entity.Property(x => x.DisplayName).HasMaxLength(120);
            entity.Property(x => x.Summary).HasMaxLength(500);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => new { x.IsEnabled, x.DisplayOrder });
        });

        builder.Entity<PortalArticle>(entity =>
        {
            entity.ToTable("articles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Slug).HasMaxLength(160);
            entity.Property(x => x.Title).HasMaxLength(240);
            entity.Property(x => x.Summary).HasMaxLength(700);
            entity.Property(x => x.Status).HasMaxLength(30);
            entity.Property(x => x.ContentMarkdown).HasColumnType("text");
            // EF includes the original version in UPDATE/DELETE so two editors cannot silently
            // overwrite each other after both loaded the same article revision.
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => new { x.Status, x.PublishedAtUtc });
            entity.HasIndex(x => new { x.CategoryId, x.PublishedAtUtc });
            entity.HasOne(x => x.Category).WithMany(x => x.Articles).HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PortalArticleMedia>(entity =>
        {
            entity.ToTable("article_media");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StoragePath).HasMaxLength(500);
            entity.Property(x => x.PublicUrl).HasMaxLength(500);
            entity.Property(x => x.OriginalFileName).HasMaxLength(260);
            entity.Property(x => x.MimeType).HasMaxLength(100);
            entity.Property(x => x.AltText).HasMaxLength(300);
            entity.Property(x => x.Caption).HasMaxLength(500);
            entity.HasIndex(x => new { x.ArticleId, x.IsCover, x.DisplayOrder });
            entity.HasOne(x => x.Article).WithMany(x => x.Media).HasForeignKey(x => x.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
