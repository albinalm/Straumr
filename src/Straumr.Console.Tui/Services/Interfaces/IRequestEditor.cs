using Straumr.Core.Models;

namespace Straumr.Console.Tui.Services.Interfaces;

public interface IRequestEditor
{
    void Run(RequestEditorContext context);
}

public sealed record RequestEditorContext(
    RequestEditorMode Mode,
    StraumrWorkspaceEntry WorkspaceEntry,
    StraumrRequest? ExistingRequest,
    Func<Task> RefreshEntries,
    Action<string> ShowSuccess,
    Action<string> ShowDanger);

public enum RequestEditorMode
{
    Create,
    Edit
}
