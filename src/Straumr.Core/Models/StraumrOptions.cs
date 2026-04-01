namespace Straumr.Core.Models;

public class StraumrOptions
{
    public string? DefaultWorkspacePath { get; set; }
    public string DefaultSecretPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".straumr", "secrets");

    public List<StraumrWorkspaceEntry> Workspaces { get; set; } = [];
    public List<StraumrSecretEntry> Secrets { get; set; } = [];
    public StraumrWorkspaceEntry? CurrentWorkspace { get; set; }
}

public class StraumrWorkspaceEntry
{
    public Guid Id { get; set; }
    public required string Path { get; set; }
}

public class StraumrSecretEntry
{
    public Guid Id { get; set; }
    public required string Path { get; set; }
}
