using Octokit;

namespace DoraDashboard.Ingestion.GitHub;

public sealed class OctokitGitHubClient : IGitHubClient
{
    private readonly GitHubClient _client;

    public OctokitGitHubClient(GitHubClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<PullRequestData>> GetMergedPullRequestsAsync(
        string owner, string repo, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var request = new PullRequestRequest { State = ItemStateFilter.Closed };
        var pullRequests = await _client.PullRequest.GetAllForRepository(owner, repo, request);

        return pullRequests
            .Where(pr => pr.Merged && pr.MergedAt is not null && pr.MergedAt >= since)
            .Select(pr => new PullRequestData(pr.Number, pr.CreatedAt, pr.MergedAt, pr.User.Login))
            .ToList();
    }

    public async Task<IReadOnlyList<DeploymentData>> GetReleasesAsync(
        string owner, string repo, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var releases = await _client.Repository.Release.GetAll(owner, repo);

        return releases
            .Where(r => r.PublishedAt is not null && r.PublishedAt >= since)
            .Select(r => new DeploymentData(r.TagName, r.PublishedAt!.Value))
            .ToList();
    }

    public async Task<IReadOnlyList<DeploymentData>> GetMainBranchMergesAsync(
        string owner, string repo, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var repository = await _client.Repository.Get(owner, repo);

        var request = new CommitRequest { Sha = repository.DefaultBranch, Since = since.UtcDateTime };
        var commits = await _client.Repository.Commit.GetAll(owner, repo, request);

        // Parents.Count > 1 is the standard signal for "this commit merged a branch" (a PR merge,
        // in GitHub's default merge-commit workflow) as opposed to a direct/fast-forward commit.
        return commits
            .Where(c => c.Parents.Count > 1)
            .Select(c => new DeploymentData(c.Sha[..7], c.Commit.Committer.Date))
            .ToList();
    }

    public async Task<IReadOnlyList<IncidentData>> GetLabeledIssuesAsync(
        string owner, string repo, IReadOnlyList<string> labels, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var request = new RepositoryIssueRequest { State = ItemStateFilter.All, Since = since.UtcDateTime };
        foreach (var label in labels)
        {
            request.Labels.Add(label);
        }

        var issues = await _client.Issue.GetAllForRepository(owner, repo, request);

        return issues
            .Select(i => new IncidentData(i.Title, i.CreatedAt, i.ClosedAt))
            .ToList();
    }
}
