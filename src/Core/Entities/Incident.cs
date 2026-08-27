namespace DoraDashboard.Core.Entities;

/// <summary>
/// An incident, detected from GitHub issues carrying one of the labels configured under
/// incidentDetection.labels in dora.config.yaml. Feeds change failure rate and MTTR.
/// </summary>
public class Incident
{
    public int Id { get; set; }

    public int RepositoryId { get; set; }
    public Repository? Repository { get; set; }

    public string Title { get; set; } = string.Empty;
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
