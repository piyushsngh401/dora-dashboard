using DoraDashboard.Core.Configuration;
using DoraDashboard.Core.Data;
using DoraDashboard.Core.Metrics;
using DoraDashboard.Ingestion;
using DoraDashboard.Ingestion.GitHub;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Octokit;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

var builder = WebApplication.CreateBuilder(args);

// --- Logging ---
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// --- dora.config.yaml: repos, teams, metric detection rules. This is what makes the tool
// configurable per org instead of hardcoded to one team's setup. ---
var configPath = builder.Configuration["Dora:ConfigPath"] ?? "dora.config.yaml";
var doraConfig = File.Exists(configPath)
    ? new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build()
        .Deserialize<DoraConfig>(File.ReadAllText(configPath))
    : new DoraConfig();
builder.Services.AddSingleton(doraConfig);

// --- Database ---
// MigrationsAssembly("Api") is required because DoraDbContext lives in Core, but Core
// deliberately has no Npgsql package reference (it stays provider-agnostic) — so the generated
// migration code, which is Npgsql-specific, has to live in Api instead of Core's own assembly.
builder.Services.AddDbContext<DoraDbContext>(options =>
    options.UseNpgsql(
        ResolveConnectionString(builder.Configuration),
        npgsql => npgsql.MigrationsAssembly("Api")));

// --- GitHub client + ingestion ---
builder.Services.AddSingleton(_ =>
{
    var client = new GitHubClient(new ProductHeaderValue("dora-dashboard"));
    var token = builder.Configuration["GitHub:Token"];
    if (!string.IsNullOrWhiteSpace(token))
    {
        client.Credentials = new Credentials(token);
    }

    return client;
});
// Fully qualified: both Octokit and our own Ingestion.GitHub namespace declare an IGitHubClient,
// and both are in scope here, so the unqualified name is ambiguous.
builder.Services.AddSingleton<DoraDashboard.Ingestion.GitHub.IGitHubClient, OctokitGitHubClient>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddHostedService<SyncSchedulerHostedService>();

// --- Metric calculators, registered as a collection so the API can iterate over whichever
// calculators are wired up without knowing about each one individually. ---
builder.Services.AddSingleton<IMetricCalculator, DeploymentFrequencyCalculator>();
builder.Services.AddSingleton<IMetricCalculator, LeadTimeForChangesCalculator>();
builder.Services.AddSingleton<IMetricCalculator, ChangeFailureRateCalculator>();
builder.Services.AddSingleton<IMetricCalculator, MeanTimeToRecoveryCalculator>();

// --- Observability ---
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("dora-dashboard-api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());

// --- Health checks ---
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DoraDbContext>("database", tags: new[] { "ready" });

// --- CORS for the React SPA ---
var webOrigin = builder.Configuration["Cors:WebOrigin"] ?? "http://localhost:5173";
builder.Services.AddCors(options => options.AddPolicy("Web", policy =>
    policy.WithOrigins(webOrigin).AllowAnyHeader().AllowAnyMethod()));

// --- API docs (also the source for the frontend's generated TypeScript client) ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- Schema bootstrap for hosted environments ---
// Local dev applies migrations explicitly via `dotnet ef database update`, but a hosted demo
// (Render, Fly.io, etc.) has no shell to run that from, so it opts in via Dora__AutoMigrate=true
// to apply pending migrations on startup instead. Off by default so local dev's workflow doesn't change.
if (app.Configuration.GetValue<bool>("Dora:AutoMigrate"))
{
    using var migrationScope = app.Services.CreateScope();
    migrationScope.ServiceProvider.GetRequiredService<DoraDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Web");

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapGet("/api/teams/{teamName}/metrics", async (
    string teamName,
    DateTimeOffset? from,
    DateTimeOffset? to,
    DoraDbContext db,
    DoraConfig config,
    IEnumerable<IMetricCalculator> calculators) =>
{
    var team = config.Teams.FirstOrDefault(t => t.Name.Equals(teamName, StringComparison.OrdinalIgnoreCase));
    if (team is null)
    {
        return Results.NotFound();
    }

    var windowStart = from ?? DateTimeOffset.UtcNow.AddDays(-30);
    var windowEnd = to ?? DateTimeOffset.UtcNow;

    var repositoryIds = await db.Repositories
        .Where(r => team.Repositories.Contains(r.Owner + "/" + r.Name))
        .Select(r => r.Id)
        .ToListAsync();

    var context = new MetricCalculationContext(
        await db.PullRequests.Where(p => repositoryIds.Contains(p.RepositoryId) && p.MergedAt >= windowStart && p.MergedAt <= windowEnd).ToListAsync(),
        await db.Deployments.Where(d => repositoryIds.Contains(d.RepositoryId) && d.DeployedAt >= windowStart && d.DeployedAt <= windowEnd).ToListAsync(),
        await db.Incidents.Where(i => repositoryIds.Contains(i.RepositoryId) && i.OpenedAt >= windowStart && i.OpenedAt <= windowEnd).ToListAsync(),
        windowStart,
        windowEnd);

    var metrics = calculators.ToDictionary(c => c.MetricType.ToString(), c => c.Calculate(context));

    return Results.Ok(new { team = team.Name, windowStart, windowEnd, metrics });
})
.WithName("GetTeamMetrics")
.WithOpenApi();

app.MapGet("/api/teams/{teamName}/metrics/series", async (
    string teamName,
    DateTimeOffset? from,
    DateTimeOffset? to,
    int? bucketDays,
    DoraDbContext db,
    DoraConfig config,
    IEnumerable<IMetricCalculator> calculators) =>
{
    var team = config.Teams.FirstOrDefault(t => t.Name.Equals(teamName, StringComparison.OrdinalIgnoreCase));
    if (team is null)
    {
        return Results.NotFound();
    }

    var windowStart = from ?? DateTimeOffset.UtcNow.AddDays(-90);
    var windowEnd = to ?? DateTimeOffset.UtcNow;
    var bucketSize = TimeSpan.FromDays(bucketDays is > 0 ? bucketDays.Value : 7);

    var repositoryIds = await db.Repositories
        .Where(r => team.Repositories.Contains(r.Owner + "/" + r.Name))
        .Select(r => r.Id)
        .ToListAsync();

    var context = new MetricCalculationContext(
        await db.PullRequests.Where(p => repositoryIds.Contains(p.RepositoryId) && p.MergedAt >= windowStart && p.MergedAt <= windowEnd).ToListAsync(),
        await db.Deployments.Where(d => repositoryIds.Contains(d.RepositoryId) && d.DeployedAt >= windowStart && d.DeployedAt <= windowEnd).ToListAsync(),
        await db.Incidents.Where(i => repositoryIds.Contains(i.RepositoryId) && i.OpenedAt >= windowStart && i.OpenedAt <= windowEnd).ToListAsync(),
        windowStart,
        windowEnd);

    // Re-runs the same IMetricCalculator strategies used by the point-in-time endpoint above,
    // once per bucket, so the trend is guaranteed to agree with the headline numbers.
    var series = MetricSeriesCalculator.GenerateSeries(context, calculators, bucketSize);

    return Results.Ok(new { team = team.Name, windowStart, windowEnd, bucketDays = bucketSize.TotalDays, series });
})
.WithName("GetTeamMetricsSeries")
.WithOpenApi();

app.MapGet("/api/repos/{repositoryId:int}/metrics", async (
    int repositoryId,
    DateTimeOffset? from,
    DateTimeOffset? to,
    DoraDbContext db,
    IEnumerable<IMetricCalculator> calculators) =>
{
    var repository = await db.Repositories.FindAsync(repositoryId);
    if (repository is null)
    {
        return Results.NotFound();
    }

    var windowStart = from ?? DateTimeOffset.UtcNow.AddDays(-30);
    var windowEnd = to ?? DateTimeOffset.UtcNow;

    var context = new MetricCalculationContext(
        await db.PullRequests.Where(p => p.RepositoryId == repositoryId && p.MergedAt >= windowStart && p.MergedAt <= windowEnd).ToListAsync(),
        await db.Deployments.Where(d => d.RepositoryId == repositoryId && d.DeployedAt >= windowStart && d.DeployedAt <= windowEnd).ToListAsync(),
        await db.Incidents.Where(i => i.RepositoryId == repositoryId && i.OpenedAt >= windowStart && i.OpenedAt <= windowEnd).ToListAsync(),
        windowStart,
        windowEnd);

    var metrics = calculators.ToDictionary(c => c.MetricType.ToString(), c => c.Calculate(context));

    return Results.Ok(new { repository = repository.FullName, windowStart, windowEnd, metrics });
})
.WithName("GetRepositoryMetrics")
.WithOpenApi();

app.MapPost("/api/sync", async (ISyncService syncService, CancellationToken cancellationToken) =>
{
    await syncService.SyncAllAsync(cancellationToken);
    return Results.Accepted();
})
.WithName("TriggerSync")
.WithOpenApi();

app.Run();

// Most hosting platforms (Render, Fly.io, Heroku-style PaaS) hand you a single postgres://
// connection URL via a DATABASE_URL env var rather than the ADO.NET keyword=value string Npgsql
// expects — so translate it when present. Local dev has no DATABASE_URL, so it keeps using
// ConnectionStrings:Default from appsettings/appsettings.Development.json exactly as before.
static string ResolveConnectionString(IConfiguration configuration)
{
    var databaseUrl = configuration["DATABASE_URL"];
    if (string.IsNullOrWhiteSpace(databaseUrl))
    {
        return configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "No database connection string configured. Set ConnectionStrings:Default (local dev) or DATABASE_URL (hosted).");
    }

    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);

    // Named distinctly from the top-level `builder` (WebApplicationBuilder) — a local function in
    // a top-level statements file shares that enclosing scope, so reusing the name would conflict.
    var connectionStringBuilder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
        SslMode = SslMode.Require,
        TrustServerCertificate = true,
    };

    return connectionStringBuilder.ConnectionString;
}

// Exposed so WebApplicationFactory<Program> in the test project can bootstrap this app in-process.
public partial class Program
{
}
