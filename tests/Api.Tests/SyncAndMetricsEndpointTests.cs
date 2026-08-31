using System.Net;
using System.Text.Json;
using Api.Tests.Fakes;
using DoraDashboard.Core.Configuration;
using DoraDashboard.Core.Data;
using DoraDashboard.Ingestion.GitHub;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Exercises POST /api/sync and GET /api/teams/{team}/metrics end-to-end — real DbContext, real
/// SyncService upsert logic, real IMetricCalculator implementations — against a throwaway Postgres
/// (Testcontainers) and a FakeGitHubClient standing in for GitHub. Before this, the only
/// integration coverage in the project was the /health check; these are the two endpoints that
/// actually matter, and IGitHubClient exists specifically to make testing them like this possible.
/// </summary>
public class SyncAndMetricsEndpointTests : IAsyncLifetime
{
    private const string TeamName = "test-team";
    private const string RepoOwner = "acme";
    private const string RepoName = "widgets";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private readonly FakeGitHubClient _fakeGitHub = new();
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var config = new DoraConfig
        {
            Teams = new List<TeamConfig>
            {
                new() { Name = TeamName, Repositories = new List<string> { $"{RepoOwner}/{RepoName}" } }
            }
        };

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DoraDbContext>>();
                services.AddDbContext<DoraDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));

                services.RemoveAll<DoraConfig>();
                services.AddSingleton(config);

                services.RemoveAll<IGitHubClient>();
                services.AddSingleton<IGitHubClient>(_fakeGitHub);
            });
        });

        _client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DoraDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Sync_ThenGetMetrics_ReturnsValuesComputedFromSyncedData()
    {
        var now = DateTimeOffset.UtcNow;
        var mergedAt = now.AddDays(-9);
        var deployedAt = mergedAt.AddHours(24); // exactly 24h lead time, for a clean assertion
        var windowStart = now.AddDays(-10);
        var windowEnd = now;

        _fakeGitHub.PullRequests = new[] { new PullRequestData(1, mergedAt.AddDays(-1), mergedAt, "octocat") };
        _fakeGitHub.Releases = new[] { new DeploymentData("v1.0.0", deployedAt) };
        _fakeGitHub.Incidents = Array.Empty<IncidentData>();

        var syncResponse = await _client!.PostAsync("/api/sync", content: null);
        Assert.Equal(HttpStatusCode.Accepted, syncResponse.StatusCode);

        var metricsResponse = await _client.GetAsync(
            $"/api/teams/{TeamName}/metrics?from={Uri.EscapeDataString(windowStart.ToString("O"))}&to={Uri.EscapeDataString(windowEnd.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, metricsResponse.StatusCode);

        using var doc = JsonDocument.Parse(await metricsResponse.Content.ReadAsStringAsync());
        var metrics = doc.RootElement.GetProperty("metrics");

        // 1 deployment over a 10-day window.
        Assert.Equal(0.1, metrics.GetProperty("DeploymentFrequency").GetDouble(), precision: 3);
        // The one PR merged exactly 24h before the one deployment.
        Assert.Equal(24.0, metrics.GetProperty("LeadTimeForChanges").GetDouble(), precision: 3);
        // No incidents synced.
        Assert.Equal(0.0, metrics.GetProperty("ChangeFailureRate").GetDouble(), precision: 3);
        Assert.Equal(0.0, metrics.GetProperty("MeanTimeToRecovery").GetDouble(), precision: 3);
    }

    [Fact]
    public async Task Sync_CalledTwiceWithTheSameData_DoesNotDuplicateRows()
    {
        var now = DateTimeOffset.UtcNow;
        _fakeGitHub.PullRequests = new[] { new PullRequestData(42, now.AddDays(-5), now.AddDays(-4), "octocat") };
        _fakeGitHub.Releases = new[] { new DeploymentData("v2.0.0", now.AddDays(-3)) };

        var first = await _client!.PostAsync("/api/sync", content: null);
        var second = await _client.PostAsync("/api/sync", content: null);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DoraDbContext>();

        // Two identical syncs should upsert, not double-insert — this is the behavior the
        // per-item AnyAsync existence checks in SyncService are there to guarantee.
        Assert.Equal(1, await db.PullRequests.CountAsync(p => p.Number == 42));
        Assert.Equal(1, await db.Deployments.CountAsync(d => d.Reference == "v2.0.0"));
        Assert.Equal(1, await db.Repositories.CountAsync(r => r.Owner == RepoOwner && r.Name == RepoName));
    }

    [Fact]
    public async Task GetMetrics_UnknownTeam_ReturnsNotFound()
    {
        var response = await _client!.GetAsync("/api/teams/does-not-exist/metrics");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
