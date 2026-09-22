namespace Straumr.Core.Models;

public class StraumrState
{
    public List<StraumrWorkspaceEntry> Workspaces { get; set; } = [];
    public List<StraumrSecretEntry> Secrets { get; set; } = [];
    public StraumrWorkspaceEntry? CurrentWorkspace { get; set; }
    public Dictionary<string, StraumrPaneLayout> PaneLayouts { get; set; } = [];
}
