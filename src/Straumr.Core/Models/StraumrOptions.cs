namespace Straumr.Core.Models;

public class StraumrOptions
{
    public string DefaultWorkspacePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".straumr", "workspaces");
    public List<StraumrWorkspaceEntry> Workspaces { get; set; } = [];
    public StraumrWorkspaceEntry? CurrentWorkspace { get; set; }
}

public class StraumrWorkspaceEntry
{
    public Guid Id { get; set; }
    public required string Path { get; set; }
}
