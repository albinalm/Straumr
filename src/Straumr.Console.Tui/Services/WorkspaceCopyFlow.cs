using Straumr.Console.Tui.Screens.Components.Shared;
using Straumr.Console.Tui.Screens.Components.Workspace;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using XenoAtom.Terminal.UI.Commands;

namespace Straumr.Console.Tui.Services;

internal sealed class WorkspaceCopyFlow(IStraumrWorkspaceService workspaces, Action<TuiCommandResultModel> notify)
{
    private Func<CancellationToken, Task>? _pending;
    public static Command Command(string id, Action execute, Func<bool> available) => new()
    {
        Id = id,
        LabelMarkup = "Copy to workspace",
        Gesture = TuiKeybindHelpers.Get(id),
        Importance = CommandImportance.Primary,
        Presentation = CommandPresentation.CommandBar,
        IsVisible = _ => available(),
        CanExecute = _ => available(),
        ConsumesGestureWhenUnavailable = false,
        Execute = _ => TuiKeybindHelpers.Run(id, execute)
    };
    public void Begin(StraumrWorkspaceEntry source, string name, Func<StraumrWorkspaceEntry, string, CancellationToken, Task> copy) =>
        Choose(source, name, destination => _pending = token => CopyAsync(destination, name, copy, token));
    public void Choose(StraumrWorkspaceEntry source, string name, Action<StraumrWorkspace> selected) =>
        _pending = token => ChooseWorkspaceAsync(source, name, selected, token);
    public async Task UpdateAsync(CancellationToken cancellationToken)
    {
        if (_pending is not { } operation)
        {
            return;
        }
        _pending = null;
        try
        {
            await operation(cancellationToken);
        }
        catch (Exception exception) when (exception is StraumrException or IOException or UnauthorizedAccessException)
        {
            notify(TuiCommandResultModel.Failed($"copy failed: {exception.Message}"));
        }
    }
    private async Task ChooseWorkspaceAsync(StraumrWorkspaceEntry source, string name,
        Action<StraumrWorkspace> selected, CancellationToken cancellationToken)
    {
        IReadOnlyList<StraumrWorkspace> available = await workspaces.ListAsync(cancellationToken);
        List<StraumrWorkspace> destinations = available.Where(workspace => workspace.Id != source.Id)
            .OrderBy(workspace => workspace.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (destinations.Count == 0)
        {
            notify(TuiCommandResultModel.Failed("Create another workspace before copying to it."));
            return;
        }
        new WorkspacePickerDialog(name, destinations, selected).Show();
    }
    private async Task CopyAsync(StraumrWorkspace destination, string name,
        Func<StraumrWorkspaceEntry, string, CancellationToken, Task> copy, CancellationToken cancellationToken)
    {
        try
        {
            await copy(workspaces.GetEntry(destination.Id), name, cancellationToken);
            notify(TuiCommandResultModel.Ok($"copied {name} to {destination.Name}"));
        }
        catch (StraumrException exception) when (exception.Reason is StraumrError.EntryConflict or StraumrError.InvalidEntry)
        {
            new TextPromptDialog("Name the copy", $"{destination.Name}: {exception.Message}. Choose a new name.", name, "Copy",
                ValidateName, renamed => _pending = token => CopyAsync(destination, renamed, copy, token)).Show();
        }
    }
    private static string? ValidateName(string name) =>
        string.IsNullOrWhiteSpace(name) ? "Enter a name." :
        name.Contains('"') ? "Names cannot contain double quotes." : null;
}
