namespace DoraDashboard.Core.Entities;

public class Repository
{
    public int Id { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public string FullName => $"{Owner}/{Name}";

    public ICollection<Team> Teams { get; set; } = new List<Team>();
    public ICollection<PullRequest> PullRequests { get; set; } = new List<PullRequest>();
    public ICollection<Deployment> Deployments { get; set; } = new List<Deployment>();
    public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
}
