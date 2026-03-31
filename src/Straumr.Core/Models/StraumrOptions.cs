namespace Straumr.Core.Models;

public class StraumrOptions
{
    public string DefaultWorkspacePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".straumr", "workspaces");
    public List<StraumrWorkspaceEntry> Workspaces { get; set; } = [];
}

public class StraumrWorkspaceEntry
{
    public required string Name { get; set; }
    public required string Path { get; set; }
    public DateTimeOffset LastAccessed { get; set; } = DateTimeOffset.UtcNow;
}
