using System.Text;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Console;
using Straumr.Console.Tui.Models;
using Straumr.Console.Tui.Screens.Base;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens;

public sealed class WorkspaceScreen(
    TuiInteractiveConsole interactiveConsole,
    IStraumrWorkspaceService workspaceService,
    IStraumrOptionsService optionsService,
    StraumrTheme theme)
    : ModelScreen<WorkspaceEntry>(theme,
        screenTitle: "Workspaces",
        hintsText: WorkspaceHintsText,
        emptyStateText: "No workspaces found",
        itemTypeNamePlural: "workspaces")
{
    private const string WorkspaceHintsText = "j/k Navigate  g/G Jump  s Set active  i Inspect  / Filter  Enter Open  : Command";

    protected override void OnInitialized(IReadOnlyList<WorkspaceEntry> entries)
    {
        int workspaceCount = optionsService.Options.Workspaces.Count;
        ShowSuccess($" {workspaceCount} workspace{(workspaceCount == 1 ? string.Empty : "s")} loaded");
    }

    protected override string GetDisplayText(WorkspaceEntry entry) => entry.Display;

    protected override bool HandleModelKeyDown(Key key, WorkspaceEntry? selectedEntry)
    {
        Rune rune = key.AsRune;
        if (key is { IsCtrl: false, IsAlt: false } && rune.Value == 's')
        {
            SetCurrentWorkspace(selectedEntry);
            return true;
        }

        return false;
    }

    private void SetCurrentWorkspace(WorkspaceEntry? selectedItem)
    {
        if (selectedItem is null)
        {
            return;
        }

        if (optionsService.Options.CurrentWorkspace != null &&
            selectedItem.StraumrEntry.Id == optionsService.Options.CurrentWorkspace?.Id)
        {
            ShowInfo($"🤔 {selectedItem.Identifier} is already the active workspace.");
            return;
        }

        if (selectedItem.IsDamaged)
        {
            ShowDanger($"😬 {selectedItem.Identifier} is damaged and cannot be set as default workspace.");
            return;
        }

        workspaceService.Activate(selectedItem.StraumrEntry.Id.ToString());
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
    }
    
    protected override void InspectSelectedEntry()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        interactiveConsole.ShowDetails(
            string.Empty,
            [
                ("ID", $"[bold]{SelectedEntry.StraumrEntry.Id}[/]"),
                ("Name", SelectedEntry.Name is not null ? $"{SelectedEntry.Identifier}" : "[secondary]N/A[/]"),
                ("Requests", SelectedEntry.RequestCount is not null ? $"{SelectedEntry.RequestCount}" : "[secondary]N/A[/]"),
                ("Secrets", SelectedEntry.SecretCount is not null ? $"{SelectedEntry.SecretCount}" : "[secondary]N/A[/]"),
                ("Authenticators", SelectedEntry.AuthCount is not null ? $"{SelectedEntry.AuthCount}" : "[secondary]N/A[/]"),
                ("Path", SelectedEntry.StraumrEntry.Path),
                ("Last Accessed", SelectedEntry.LastAccessed?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[warning]N/A[/]"),
                ("Status", SelectedEntry.IsDamaged ? "[danger][bold]Damaged[/][/]" : "[success][bold]Valid[/][/]")
            ]);
    }

    protected override async Task<IReadOnlyList<WorkspaceEntry>> LoadEntriesAsync(CancellationToken cancellationToken)
    {
        if (optionsService.Options.Workspaces.Count == 0)
        {
            return [];
        }

        var items = new List<WorkspaceEntry>();

        foreach (StraumrWorkspaceEntry entry in optionsService.Options.Workspaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WorkspaceEntry line = await BuildWorkspaceEntryAsync(entry);
            items.Add(line);
        }

        return items
            .OrderByDescending(item => item.LastAccessed)
            .ToList();
    }

    private async Task<WorkspaceEntry> BuildWorkspaceEntryAsync(StraumrWorkspaceEntry entry)
    {
        var lineBuilder = new StringBuilder();
        DateTimeOffset? lastAccessed = null;
        var isDamaged = false;
        string identifier;
        string? name = null;
        var status = "Valid";
        int? requests = null;
        int? secrets = null;
        int? auths = null;
        
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
            requests = workspace.Requests.Count;
            secrets = workspace.Secrets.Count;
            auths = workspace.Auths.Count;
            name = workspace.Name;
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            lineBuilder.Append($"{entry.Id} [Corrupt] ");
            isDamaged = true;
            identifier = entry.Id.ToString();
            status = "Corrupt";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.EntryNotFound)
        {
            lineBuilder.Append($"{entry.Id} [Missing] ");
            isDamaged = true;
            identifier = entry.Id.ToString();
            status = "Missing";
        }
        
        return new WorkspaceEntry
        {
            StraumrEntry = entry,
            Display = lineBuilder.ToString(),
            Identifier = identifier,
            Status = status,
            IsDamaged = isDamaged,
            RequestCount = requests,
            SecretCount = secrets,
            AuthCount = auths,
            LastAccessed = lastAccessed,
            Name = name
        };
    }
}
