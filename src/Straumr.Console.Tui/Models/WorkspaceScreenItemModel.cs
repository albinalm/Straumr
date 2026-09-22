using System.Diagnostics.CodeAnalysis;
using Straumr.Core.Models;

namespace Straumr.Console.Tui.Models;

public sealed record WorkspaceScreenItemModel(
    StraumrWorkspace? Workspace,
    StraumrWorkspaceEntry Entry,
    string? Corruption = null)
{
    public Guid Id => Entry.Id;

    [MemberNotNullWhen(true, nameof(Corruption))]
    [MemberNotNullWhen(false, nameof(Workspace))]
    public bool IsCorrupt => Workspace is null;

    public string Name
    {
        get
        {
            if (Workspace is not null)
            {
                return Workspace.Name;
            }

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

    public string? ContainingDirectory =>
        Path.GetDirectoryName(Path.GetDirectoryName(Entry.Path));
}
