using System.Diagnostics.CodeAnalysis;
using Straumr.Console.Tui.Formatting;
using Straumr.Core.Models;

namespace Straumr.Console.Tui.Screens.Workspace;

/// <remarks>
/// Which workspace is current is deliberately not a field here. It changes while the screen is live,
/// so the screen holds it as state and the row is derived from that; a value baked in at load would
/// go stale the moment a workspace was activated.
/// </remarks>
/// <param name="Workspace">
/// The workspace the entry's file holds, or null when that file is not one. A registry entry whose
/// file cannot be read stays on the list rather than vanishing from it, so whatever broke it is
/// visible and can be edited again.
/// </param>
/// <param name="Corruption">
/// Why the file could not be read, as a lowercase phrase the screen puts after "cannot be read". Null
/// exactly when <paramref name="Workspace"/> is not.
/// </param>
public sealed record WorkspaceScreenItem(
    StraumrWorkspace? Workspace,
    StraumrWorkspaceEntry Entry,
    string? Corruption = null)
{
    /// <summary>The registry's ID, which is the one the screen and Core address the workspace by.</summary>
    public Guid Id => Entry.Id;

    [MemberNotNullWhen(true, nameof(Corruption))]
    [MemberNotNullWhen(false, nameof(Workspace))]
    public bool IsCorrupt => Workspace is null;

    /// <remarks>
    /// A workspace whose file cannot be read has no name to show, so the folder Core named after it
    /// stands in: it lays workspaces out as <c>{output}/{name}/{id}.straumr</c>.
    /// </remarks>
    public string Name
    {
        get
        {
            if (Workspace is not null)
                return Workspace.Name;

            string? folder = Path.GetFileName(Path.GetDirectoryName(Entry.Path));
            return string.IsNullOrEmpty(folder)
                ? Path.GetFileNameWithoutExtension(Entry.Path)
                : folder;
        }
    }

    public string ShortId => Id.ToString("N")[..8];

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
