using Straumr.Console.Tui.Formatting;
using Straumr.Core.Models;

namespace Straumr.Console.Tui.Screens.Workspace;

/// <remarks>
/// Which workspace is current is deliberately not a field here. It changes while the screen is live,
/// so the screen holds it as state and the row is derived from that; a value baked in at load would
/// go stale the moment a workspace was activated.
/// </remarks>
public sealed record WorkspaceScreenItem(
    StraumrWorkspace Workspace,
    StraumrWorkspaceEntry Entry)
{
    public string ShortId => Workspace.Id.ToString("N")[..8];

    public string DisplayPath => PathFormatting.Display(Entry.Path);

    public string DisplayDirectory =>
        PathFormatting.Display(Path.GetDirectoryName(Entry.Path) ?? Entry.Path);

    /// <summary>
    /// The folder the workspace's own folder sits in, which is the output directory Core composes a
    /// workspace path from: it lays them out as <c>{output}/{name}/{id}.straumr</c>. So this is where
    /// a sibling of this workspace would be written, one level above <see cref="DisplayDirectory"/>.
    /// </summary>
    public string? ContainingDirectory =>
        Path.GetDirectoryName(Path.GetDirectoryName(Entry.Path));
}
