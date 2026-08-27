using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DoraDashboard.Ingestion;

/// <summary>Runs SyncAllAsync on a fixed interval. POST /api/sync triggers the same logic on demand.</summary>
public sealed class SyncSchedulerHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;

    public SyncSchedulerHostedService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
            await syncService.SyncAllAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
