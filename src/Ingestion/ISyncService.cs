namespace DoraDashboard.Ingestion;

public interface ISyncService
{
    /// <summary>Syncs every repository referenced by dora.config.yaml.</summary>
    Task SyncAllAsync(CancellationToken cancellationToken = default);

    Task SyncRepositoryAsync(string owner, string name, CancellationToken cancellationToken = default);
}
