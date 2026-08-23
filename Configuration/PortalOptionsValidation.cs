using Microsoft.Extensions.Options;

namespace MelrandiaManagement.Configuration;

/// <summary>Fails startup early when a database address cannot be used safely.</summary>
public sealed class PostgreSqlOptionsValidator : IValidateOptions<PostgreSqlOptions>
{
    public ValidateOptionsResult Validate(string? name, PostgreSqlOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Host)) errors.Add("PostgreSql:Host không được để trống.");
        if (options.Port is < 1 or > 65535) errors.Add("PostgreSql:Port phải trong khoảng 1-65535.");
        if (string.IsNullOrWhiteSpace(options.DatabaseName)) errors.Add("PostgreSql:DatabaseName không được để trống.");
        if (string.IsNullOrWhiteSpace(options.Username)) errors.Add("PostgreSql:Username không được để trống.");
        if (string.IsNullOrWhiteSpace(options.Password)) errors.Add("PostgreSql:Password không được để trống.");
        if (options.CommandTimeoutSeconds is < 1 or > 300) errors.Add("PostgreSql:CommandTimeoutSeconds phải trong khoảng 1-300.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

/// <summary>Validates non-secret bootstrap identity fields; the initializer validates password conditionally.</summary>
public sealed class BootstrapAdminOptionsValidator : IValidateOptions<BootstrapAdminOptions>
{
    public ValidateOptionsResult Validate(string? name, BootstrapAdminOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Username) || options.Username.Length is < 3 or > 64)
            return ValidateOptionsResult.Fail("BootstrapAdmin:Username phải có 3-64 ký tự.");
        if (string.IsNullOrWhiteSpace(options.DisplayName) || options.DisplayName.Length > 120)
            return ValidateOptionsResult.Fail("BootstrapAdmin:DisplayName phải có 1-120 ký tự.");
        if (!string.IsNullOrEmpty(options.Password) && options.Password.Length < 12)
            return ValidateOptionsResult.Fail("BootstrapAdmin:Password phải có ít nhất 12 ký tự.");
        return ValidateOptionsResult.Success;
    }
}

/// <summary>Prevents an accidental tight health-check loop or unbounded request timeout.</summary>
public sealed class InitialProjectOptionsValidator : IValidateOptions<InitialProjectOptions>
{
    public ValidateOptionsResult Validate(string? name, InitialProjectOptions options)
    {
        var errors = new List<string>();
        if (!ValidHttpUrl(options.AmsPublicUrl)) errors.Add("InitialProjects:AmsPublicUrl phải là HTTP/HTTPS URL tuyệt đối.");
        if (!ValidHttpUrl(options.AmsHealthUrl)) errors.Add("InitialProjects:AmsHealthUrl phải là HTTP/HTTPS URL tuyệt đối.");
        if (options.HealthCheckIntervalSeconds is < 10 or > 3600) errors.Add("HealthCheckIntervalSeconds phải trong khoảng 10-3600.");
        if (options.HealthCheckTimeoutSeconds is < 1 or > 30) errors.Add("HealthCheckTimeoutSeconds phải trong khoảng 1-30.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static bool ValidHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

/// <summary>Bounds cache staleness and prevents accidental zero-duration refresh loops.</summary>
public sealed class PortalPerformanceOptionsValidator : IValidateOptions<PortalPerformanceOptions>
{
    public ValidateOptionsResult Validate(string? name, PortalPerformanceOptions options) =>
        options.PublicProjectCacheSeconds is >= 5 and <= 300
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "PortalPerformance:PublicProjectCacheSeconds phải trong khoảng 5-300.");
}

/// <summary>Bounds upload size/count and rejects a missing persistent-storage path.</summary>
public sealed class ArticleMediaOptionsValidator : IValidateOptions<ArticleMediaOptions>
{
    public ValidateOptionsResult Validate(string? name, ArticleMediaOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.RootPath)) errors.Add("ArticleMedia:RootPath không được để trống.");
        if (options.MaxFileSizeMegabytes is < 1 or > 25) errors.Add("MaxFileSizeMegabytes phải trong khoảng 1-25.");
        if (options.MaxFilesPerArticle is < 1 or > 20) errors.Add("MaxFilesPerArticle phải trong khoảng 1-20.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
