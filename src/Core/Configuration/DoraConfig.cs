namespace DoraDashboard.Core.Configuration;

/// <summary>
/// Root of dora.config.yaml — the single file that makes this tool configurable per org rather
/// than hardcoded to one team's repos and conventions.
/// </summary>
public class DoraConfig
{
    public List<TeamConfig> Teams { get; set; } = new();
    public DeploymentDetectionConfig DeploymentDetection { get; set; } = new();
    public IncidentDetectionConfig IncidentDetection { get; set; } = new();
}

public class TeamConfig
{
    public string Name { get; set; } = string.Empty;

    /// <summary>"owner/repo" entries.</summary>
    public List<string> Repositories { get; set; } = new();
}

public class DeploymentDetectionConfig
{
    /// <summary>"github-release" | "main-merge" | "workflow-run". Only "github-release" is implemented in Phase 0/1.</summary>
    public string Strategy { get; set; } = "github-release";

    public string? WorkflowName { get; set; }
}

public class IncidentDetectionConfig
{
    public List<string> Labels { get; set; } = new() { "incident" };
}
