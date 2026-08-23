using MelrandiaManagement.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MelrandiaManagement.Infrastructure;

/// <summary>Readiness check proving that MM can open its own PostgreSQL database.</summary>
public sealed class PostgreSqlHealthCheck(IDbContextFactory<ManagementDbContext> factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL MM có thể truy cập.")
                : HealthCheckResult.Unhealthy("PostgreSQL MM không thể truy cập.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL MM health check thất bại.", exception);
        }
    }
}
