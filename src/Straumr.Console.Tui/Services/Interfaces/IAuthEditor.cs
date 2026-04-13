using Straumr.Core.Models;

namespace Straumr.Console.Tui.Services.Interfaces;

public interface IAuthEditor
{
    void Run(AuthEditorContext context);
}

public sealed record AuthEditorContext(
    AuthEditorMode Mode,
    StraumrWorkspaceEntry WorkspaceEntry,
    StraumrAuth? ExistingAuth,
    Func<Task> RefreshEntries,
    Action<string> ShowSuccess,
    Action<string> ShowDanger);

public enum AuthEditorMode
{
    Create,
    Edit
}
