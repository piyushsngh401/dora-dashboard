namespace DoraDashboard.Core.Entities;

/// <summary>
/// A merged (or open) pull request pulled from GitHub. Used to compute lead time for changes.
/// </summary>
public class PullRequest
{
    public int Id { get; set; }

    public int RepositoryId { get; set; }
    public Repository? Repository { get; set; }

    public int Number { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? MergedAt { get; set; }
    public string AuthorLogin { get; set; } = string.Empty;
}
