namespace DoraDashboard.Core.Entities;

/// <summary>
/// A single deployment event, detected according to the strategy configured in dora.config.yaml
/// (GitHub Release by default; merges to main or tagged workflow runs are alternate strategies).
/// </summary>
public class Deployment
{
    public int Id { get; set; }

    public int RepositoryId { get; set; }
    public Repository? Repository { get; set; }

    /// <summary>Tag name or commit SHA that identifies what was deployed.</summary>
    public string Reference { get; set; } = string.Empty;

    public DateTimeOffset DeployedAt { get; set; }

    /// <summary>Which detection strategy produced this record, e.g. "release", "main-merge".</summary>
    public string Source { get; set; } = string.Empty;
}
