using DoraDashboard.Core.Configuration;
using DoraDashboard.Core.Data;
using DoraDashboard.Core.Entities;
using DoraDashboard.Ingestion.GitHub;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;

namespace DoraDashboard.Ingestion;

/// <summary>
/// Pulls PRs, releases, and labeled issues for the repos in dora.config.yaml and upserts them.
/// Called both by the periodic scheduler and by POST /api/sync, so the on-demand and background
/// paths never drift apart.
/// </summary>
public sealed class SyncService : ISyncService
{
    private readonly DoraDbContext _db;
    private readonly IGitHubClient _gitHub;
    private readonly DoraConfig _config;
    private readonly ResiliencePipeline _resilience;

    public SyncService(DoraDbContext db, IGitHubClient gitHub, DoraConfig config)
    {
        _db = db;
        _gitHub = gitHub;
        _config = config;
        _resilience = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2)
            })
            .Build();
    }

    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var repositoryFullNames = _config.Teams
            .SelectMany(t => t.Repositories)
            .Distinct();

        foreach (var fullName in repositoryFullNames)
        {
            var parts = fullName.Split('/', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            await SyncRepositoryAsync(parts[0], parts[1], cancellationToken);
        }
    }

    public async Task SyncRepositoryAsync(string owner, string name, CancellationToken cancellationToken = default)
    {
        var repository = await _db.Repositories
            .FirstOrDefaultAsync(r => r.Owner == owner && r.Name == name, cancellationToken);

        if (repository is null)
        {
            repository = new Repository { Owner = owner, Name = name };
            _db.Repositories.Add(repository);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // v1 window: 90 days back. Wide enough to backfill a fresh repo, cheap enough to stay
        // under GitHub's rate limits for a handful of repos.
        var since = DateTimeOffset.UtcNow.AddDays(-90);

        var pullRequests = await _resilience.ExecuteAsync(
            async ct => await _gitHub.GetMergedPullRequestsAsync(owner, name, since, ct), cancellationToken);

        var deployments = await _resilience.ExecuteAsync(
            async ct => await FetchDeploymentsAsync(owner, name, since, ct), cancellationToken);

        var incidents = await _resilience.ExecuteAsync(
            async ct => await _gitHub.GetLabeledIssuesAsync(owner, name, _config.IncidentDetection.Labels, since, ct), cancellationToken);

        foreach (var pr in pullRequests)
        {
            var exists = await _db.PullRequests.AnyAsync(
                p => p.RepositoryId == repository.Id && p.Number == pr.Number, cancellationToken);
            if (exists)
            {
                continue;
            }

            _db.PullRequests.Add(new PullRequest
            {
                RepositoryId = repository.Id,
                Number = pr.Number,
                CreatedAt = pr.CreatedAt,
                MergedAt = pr.MergedAt,
                AuthorLogin = pr.AuthorLogin
            });
        }

        foreach (var deployment in deployments)
        {
            var exists = await _db.Deployments.AnyAsync(
                d => d.RepositoryId == repository.Id && d.Reference == deployment.Reference, cancellationToken);
            if (exists)
            {
                continue;
            }

            _db.Deployments.Add(new Deployment
            {
                RepositoryId = repository.Id,
                Reference = deployment.Reference,
                DeployedAt = deployment.DeployedAt,
                Source = _config.DeploymentDetection.Strategy
            });
        }

        foreach (var incident in incidents)
        {
            var exists = await _db.Incidents.AnyAsync(
                i => i.RepositoryId == repository.Id && i.Title == incident.Title && i.OpenedAt == incident.OpenedAt,
                cancellationToken);
            if (exists)
            {
                continue;
            }

            _db.Incidents.Add(new Incident
            {
                RepositoryId = repository.Id,
                Title = incident.Title,
                OpenedAt = incident.OpenedAt,
                ResolvedAt = incident.ResolvedAt
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // Previously dora.config.yaml advertised three strategies here (github-release, main-merge,
    // workflow-run) but the code silently ignored the setting and always used releases — a
    // configuration option that did nothing regardless of what you set it to. This now actually
    // branches on it, and fails loudly for a strategy that isn't implemented yet rather than
    // quietly falling back to the wrong behavior.
    private Task<IReadOnlyList<DeploymentData>> FetchDeploymentsAsync(
        string owner, string name, DateTimeOffset since, CancellationToken cancellationToken) =>
        _config.DeploymentDetection.Strategy switch
        {
            "github-release" => _gitHub.GetReleasesAsync(owner, name, since, cancellationToken),
            "main-merge" => _gitHub.GetMainBranchMergesAsync(owner, name, since, cancellationToken),
            var strategy => throw new NotSupportedException(
                $"deploymentDetection.strategy '{strategy}' is not implemented yet. Supported values: github-release, main-merge.")
        };
}
