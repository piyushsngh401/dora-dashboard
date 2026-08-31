using DoraDashboard.Ingestion.GitHub;

namespace Api.Tests.Fakes;

/// <summary>
/// Canned GitHub responses for integration tests. IGitHubClient exists specifically so
/// SyncService can be exercised end-to-end — through the real DbContext, the real upsert logic,
/// the real metric calculators — without making an actual GitHub API call or needing a token.
/// Every method ignores its arguments and returns whatever the test configured; that's enough
/// here since these tests only ever point at one fake "repository".
/// </summary>
public sealed class FakeGitHubClient : IGitHubClient
{
    public IReadOnlyList<PullRequestData> PullRequests { get; set; } = Array.Empty<PullRequestData>();
    public IReadOnlyList<DeploymentData> Releases { get; set; } = Array.Empty<DeploymentData>();
    public IReadOnlyList<DeploymentData> MainBranchMerges { get; set; } = Array.Empty<DeploymentData>();
    public IReadOnlyList<IncidentData> Incidents { get; set; } = Array.Empty<IncidentData>();

    public Task<IReadOnlyList<PullRequestData>> GetMergedPullRequestsAsync(
        string owner, string repo, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        Task.FromResult(PullRequests);

    public Task<IReadOnlyList<DeploymentData>> GetReleasesAsync(
        string owner, string repo, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        Task.FromResult(Releases);

    public Task<IReadOnlyList<DeploymentData>> GetMainBranchMergesAsync(
        string owner, string repo, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        Task.FromResult(MainBranchMerges);

    public Task<IReadOnlyList<IncidentData>> GetLabeledIssuesAsync(
        string owner, string repo, IReadOnlyList<string> labels, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        Task.FromResult(Incidents);
}
