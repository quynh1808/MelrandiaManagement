namespace MelrandiaManagement.Configuration;

/// <summary>Strongly typed PostgreSQL connection settings supplied by environment in production.</summary>
public sealed class PostgreSqlOptions
{
    public const string SectionName = "PostgreSql";
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5432;
    public string DatabaseName { get; set; } = "melrandia_management";
    public string Username { get; set; } = "melrandia_app";
    public string Password { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; } = 30;
    public bool AutoMigrate { get; set; } = true;
}

/// <summary>
/// Controls the one-time administrator bootstrap. The password is required only while the user
/// database is empty and must be supplied through an environment variable, never committed.
/// </summary>
public sealed class BootstrapAdminOptions
{
    public const string SectionName = "BootstrapAdmin";
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Quản trị Melrandia";
}

/// <summary>Initial AMS endpoint used only when the Project Registry has no AMS record.</summary>
public sealed class InitialProjectOptions
{
    public const string SectionName = "InitialProjects";
    public string AmsPublicUrl { get; set; } = "http://localhost:5080";
    public string AmsHealthUrl { get; set; } = "http://localhost:5080/health/ready";
    public int HealthCheckIntervalSeconds { get; set; } = 30;
    public int HealthCheckTimeoutSeconds { get; set; } = 5;
}

/// <summary>
/// Performance controls that are safe to tune without changing business data.
/// Public registry cache is process-local and deliberately short-lived.
/// </summary>
public sealed class PortalPerformanceOptions
{
    public const string SectionName = "PortalPerformance";
    public int PublicProjectCacheSeconds { get; set; } = 30;
}

/// <summary>
/// Persistent article-image storage. RootPath may be relative to ContentRoot on Windows and is
/// mounted as a Docker volume in production so container replacement never deletes uploads.
/// </summary>
public sealed class ArticleMediaOptions
{
    public const string SectionName = "ArticleMedia";
    public string RootPath { get; set; } = "App_Data/media";
    public int MaxFileSizeMegabytes { get; set; } = 8;
    public int MaxFilesPerArticle { get; set; } = 8;
}
