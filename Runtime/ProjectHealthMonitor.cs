using System.Diagnostics;
using MelrandiaManagement.Configuration;
using MelrandiaManagement.Data;
using MelrandiaManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MelrandiaManagement.Runtime;

/// <summary>
/// Polls only administrator-defined health URLs. Failure updates the registry but never makes MM
/// unavailable, preserving the portal when one child project is offline.
/// </summary>
public sealed class ProjectHealthMonitor(
    IDbContextFactory<ManagementDbContext> factory,
    IHttpClientFactory httpClientFactory,
    IOptions<InitialProjectOptions> options,
    ILogger<ProjectHealthMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.HealthCheckIntervalSeconds));
        do
        {
            try
            {
                await CheckAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // A temporary MM database/network outage must not permanently stop
                // the monitor. Readiness reports the database failure separately.
                logger.LogError(exception, "Chu kỳ project health thất bại; sẽ thử lại ở chu kỳ sau.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CheckAllAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var projects = await db.Projects
            .Where(x => x.IsEnabled && x.HealthUrl != string.Empty)
            .ToListAsync(cancellationToken);
        var client = httpClientFactory.CreateClient("project-health");

        foreach (var project in projects)
        {
            var previous = project.HealthStatus;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var response = await client.GetAsync(project.HealthUrl,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                project.HealthStatus = response.IsSuccessStatusCode ? "Healthy" : "Unavailable";
                project.LastHealthError = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                project.HealthStatus = "Unavailable";
                project.LastHealthError = exception.Message.Length > 300
                    ? exception.Message[..300]
                    : exception.Message;
            }

            stopwatch.Stop();
            project.LastHealthCheckAtUtc = DateTime.UtcNow;
            project.LastResponseMilliseconds = stopwatch.ElapsedMilliseconds;
            project.UpdatedAtUtc = DateTime.UtcNow;
            if (!previous.Equals(project.HealthStatus, StringComparison.Ordinal))
            {
                if (project.HealthStatus == "Unavailable")
                    logger.LogWarning("Project {ProjectKey} chuyển sang Unavailable: {Message}",
                        project.ProjectKey, project.LastHealthError);
                else
                    logger.LogInformation("Project {ProjectKey} chuyển sang {HealthStatus} trong {ElapsedMs}ms.",
                        project.ProjectKey, project.HealthStatus, project.LastResponseMilliseconds);

                db.AuditLogs.Add(new PortalAuditLog
                {
                    Actor = "project-health-monitor",
                    Action = "project.health-changed",
                    EntityType = "project",
                    EntityId = project.Id.ToString(),
                    Detail = $"{previous} -> {project.HealthStatus}"
                });
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
