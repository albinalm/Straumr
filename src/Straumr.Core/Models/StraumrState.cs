namespace Straumr.Core.Models;

public class StraumrState
{
    public List<StraumrWorkspaceEntry> Workspaces { get; set; } = [];
    public List<StraumrSecretEntry> Secrets { get; set; } = [];
    public StraumrWorkspaceEntry? CurrentWorkspace { get; set; }
    public Dictionary<string, StraumrPaneLayout> PaneLayouts { get; set; } = [];
}

public class StraumrPaneLayout
{
    public int Panels { get; set; } = 31;
    public int Sections { get; set; } = 48;
    public int Stack { get; set; } = 50;
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
