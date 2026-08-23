using MelrandiaManagement.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace MelrandiaManagement.Data;

/// <summary>Builds a connection string without placing database credentials in source JSON.</summary>
public static class ManagementDbConnection
{
    public static string Build(PostgreSqlOptions options) => new NpgsqlConnectionStringBuilder
    {
        Host = options.Host,
        Port = options.Port,
        Database = options.DatabaseName,
        Username = options.Username,
        Password = options.Password,
        CommandTimeout = options.CommandTimeoutSeconds,
        Timeout = Math.Min(options.CommandTimeoutSeconds, 30),
        Pooling = true,
        ApplicationName = "MelrandiaManagement"
    }.ConnectionString;
}

/// <summary>
/// Supplies dotnet-ef with the same non-secret defaults and secret precedence as the web host.
/// A placeholder password remains available for model-only CI commands that never open a database.
/// </summary>
public sealed class ManagementDbContextDesignFactory : IDesignTimeDbContextFactory<ManagementDbContext>
{
    public ManagementDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<ManagementDbContextDesignFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var settings = configuration.GetSection(PostgreSqlOptions.SectionName)
            .Get<PostgreSqlOptions>() ?? new PostgreSqlOptions();

        // Migration creation/model-drift checks need provider metadata but do
        // not connect. Database update uses User Secrets or environment value.
        if (string.IsNullOrWhiteSpace(settings.Password))
            settings.Password = "design-time-only";

        var options = new DbContextOptionsBuilder<ManagementDbContext>()
            .UseNpgsql(ManagementDbConnection.Build(settings))
            .Options;
        return new ManagementDbContext(options);
    }
}
