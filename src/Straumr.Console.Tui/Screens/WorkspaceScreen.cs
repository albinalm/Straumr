using System.Text;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Console;
using Straumr.Console.Tui.Screens.Base;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens;

public sealed class WorkspaceScreen(
    TuiInteractiveConsole interactiveConsole,
    IStraumrWorkspaceService workspaceService,
    IStraumrOptionsService optionsService,
    StraumrTheme theme)
    : ModelScreen<WorkspaceScreen.WorkspaceItem>(theme,
        screenTitle: "Workspaces",
        hintsText: WorkspaceHintsText,
        emptyStateText: "No workspaces found",
        itemTypeNamePlural: "workspaces")
{
    private const string WorkspaceHintsText = "j/k Navigate  g/G Jump  s Set active  / Filter  Enter Open  : Command";

    protected override void OnInitialized(IReadOnlyList<WorkspaceItem> entries)
    {
        int workspaceCount = optionsService.Options.Workspaces.Count;
        ShowSuccess($" {workspaceCount} workspace{(workspaceCount == 1 ? string.Empty : "s")} loaded");
    }

    protected override string GetDisplayText(WorkspaceItem entry) => entry.Display;

    protected override bool HandleModelKeyDown(Key key, WorkspaceItem? selectedEntry)
    {
        Rune rune = key.AsRune;
        if (key is { IsCtrl: false, IsAlt: false } && rune.Value == 's')
        {
            SetCurrentWorkspace(selectedEntry);
            return true;
        }

        return false;
    }

    private void SetCurrentWorkspace(WorkspaceItem? selectedItem)
    {
        if (selectedItem is null)
        {
            return;
        }

        if (optionsService.Options.CurrentWorkspace != null &&
            selectedItem.Entry.Id == optionsService.Options.CurrentWorkspace?.Id)
        {
            ShowInfo($"🤔 {selectedItem.Identifier} is already the active workspace.");
            return;
        }

        if (selectedItem.IsDamaged)
        {
            ShowDanger($"😬 {selectedItem.Identifier} is damaged and cannot be set as default workspace.");
            return;
        }

        workspaceService.Activate(selectedItem.Entry.Id.ToString());
        ShowSuccess($"😎 {selectedItem.Identifier} is now the active workspace.");
    }

    protected override IEnumerable<ModelCommand> GetCommands()
    {
        yield return new ModelCommand("set", "Set selected workspace as active", _ => SetCurrentWorkspace(SelectedEntry), "use");
    }

    protected override void OpenSelectedEntry()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        ShowWorkspaceChildren();
    }

    private void ShowWorkspaceChildren()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        interactiveConsole.ShowDetails(
            string.Empty,
            [
                ("ID", $"[bold]{SelectedEntry.Entry.Id}[/]"),
                ("Name", SelectedEntry.Identifier is not null ? $"{SelectedEntry.Identifier}" : "[secondary]N/A[/]"),
                ("Path", SelectedEntry.Entry.Path),
                ("Status", SelectedEntry.IsDamaged ? "[danger][bold]Damaged[/][/]" : "[success][bold]Valid[/][/]"),
                ("Last Accessed", SelectedEntry.LastAccessed?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[warning]N/A[/]"),
            ]);
    }

    protected override async Task<IReadOnlyList<WorkspaceItem>> LoadEntriesAsync(CancellationToken cancellationToken)
    {
        if (optionsService.Options.Workspaces.Count == 0)
        {
            return [];
        }

        var items = new List<WorkspaceItem>();

        foreach (StraumrWorkspaceEntry entry in optionsService.Options.Workspaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WorkspaceItem line = await BuildWorkspaceItemAsync(entry);
            items.Add(line);
        }

        return items
            .OrderByDescending(item => item.LastAccessed)
            .ToList();
    }

    private async Task<WorkspaceItem> BuildWorkspaceItemAsync(StraumrWorkspaceEntry entry)
    {
        var lineBuilder = new StringBuilder();
        DateTimeOffset? lastAccessed = null;
        var isDamaged = false;
        string identifier;
        if (optionsService.Options.CurrentWorkspace != null && entry.Id == optionsService.Options.CurrentWorkspace.Id)
        {
            lineBuilder.Append("(Current) ");
        }

        try
        {
            StraumrWorkspace workspace = await workspaceService.PeekWorkspace(entry.Path);
            lineBuilder.Append(workspace.Name);
            lastAccessed = workspace.LastAccessed;
            identifier = workspace.Name;
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            lineBuilder.Append($"{entry.Id} [Corrupt] ");
            isDamaged = true;
            identifier = entry.Id.ToString();
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.EntryNotFound)
        {
            lineBuilder.Append($"{entry.Id} [Missing] ");
            isDamaged = true;
            identifier = entry.Id.ToString();
        }

        return new WorkspaceItem(entry, lineBuilder.ToString(), identifier, isDamaged, lastAccessed);
    }

    public sealed record WorkspaceItem(
        StraumrWorkspaceEntry Entry,
        string Display,
        string? Identifier,
        bool IsDamaged,
        DateTimeOffset? LastAccessed);
}
