using DoraDashboard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DoraDashboard.Core.Data;

public class DoraDbContext : DbContext
{
    public DoraDbContext(DbContextOptions<DoraDbContext> options) : base(options)
    {
    }

    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<PullRequest> PullRequests => Set<PullRequest>();
    public DbSet<Deployment> Deployments => Set<Deployment>();
    public DbSet<Incident> Incidents => Set<Incident>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Repository>()
            .HasIndex(r => new { r.Owner, r.Name })
            .IsUnique();

        modelBuilder.Entity<Team>()
            .HasMany(t => t.Repositories)
            .WithMany(r => r.Teams)
            .UsingEntity(j => j.ToTable("TeamRepositories"));

        modelBuilder.Entity<PullRequest>()
            .HasIndex(p => new { p.RepositoryId, p.Number })
            .IsUnique();

        modelBuilder.Entity<Deployment>()
            .HasIndex(d => new { d.RepositoryId, d.Reference })
            .IsUnique();
    }
}
