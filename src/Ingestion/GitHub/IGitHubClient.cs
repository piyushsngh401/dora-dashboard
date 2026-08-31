namespace DoraDashboard.Ingestion.GitHub;

/// <summary>
/// Everything the sync service needs from GitHub, behind an interface so it can be mocked in
/// tests without hitting the real API. OctokitGitHubClient is the only implementation for now.
/// </summary>
public interface IGitHubClient
{
    Task<IReadOnlyList<PullRequestData>> GetMergedPullRequestsAsync(
        string owner, string repo, DateTimeOffset since, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeploymentData>> GetReleasesAsync(
        string owner, string repo, DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>
    /// The "main-merge" deployment-detection strategy: treats each merge commit on the default
    /// branch as a deployment. A reasonable proxy for teams practicing continuous deployment from
    /// main without cutting GitHub Releases — the same shape of data (a reference + a timestamp),
    /// just sourced from commit history instead of the Releases API.
    /// </summary>
    Task<IReadOnlyList<DeploymentData>> GetMainBranchMergesAsync(
        string owner, string repo, DateTimeOffset since, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncidentData>> GetLabeledIssuesAsync(
        string owner, string repo, IReadOnlyList<string> labels, DateTimeOffset since, CancellationToken cancellationToken = default);
}

public sealed record PullRequestData(int Number, DateTimeOffset CreatedAt, DateTimeOffset? MergedAt, string AuthorLogin);

public sealed record DeploymentData(string Reference, DateTimeOffset DeployedAt);

public sealed record IncidentData(string Title, DateTimeOffset OpenedAt, DateTimeOffset? ResolvedAt);
