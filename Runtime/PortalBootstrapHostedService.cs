using MelrandiaManagement.Data;

namespace MelrandiaManagement.Runtime;

/// <summary>Blocks other hosted workers until migration, roles and bootstrap records are ready.</summary>
public sealed class PortalBootstrapHostedService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PortalDatabaseInitializer>()
            .InitializeAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
