using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DoraDashboard.Ingestion;

/// <summary>
/// Runs SyncAllAsync on a fixed interval. POST /api/sync triggers the same logic on demand.
/// A sync failure (Postgres unreachable, GitHub rate-limited, bad config, etc.) is logged and
/// retried on the next interval rather than rethrown: by default, an unhandled exception in a
/// BackgroundService is fatal to the *entire host*, not just this service, so without this
/// try/catch a transient failure here would take the whole API process down.
/// </summary>
public sealed class SyncSchedulerHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SyncSchedulerHostedService> _logger;

    public SyncSchedulerHostedService(IServiceScopeFactory scopeFactory, ILogger<SyncSchedulerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();

            try
            {
                await syncService.SyncAllAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Background sync failed; will retry on the next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
