using System.Diagnostics;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Console;
using Straumr.Console.Tui.Helpers;
using Straumr.Console.Tui.Components.Prompts.Form;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Models;
using Straumr.Console.Tui.Screens.Base;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens;

public sealed class RequestsScreen(
    TuiInteractiveConsole interactiveConsole,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService,
    IStraumrAuthService authService,
    IStraumrOptionsService optionsService,
    ScreenNavigationContext navigationContext,
    StraumrTheme theme)
    : ModelScreen<RequestEntry>(theme,
        screenTitle: "Requests",
        emptyStateText: "No requests found",
        itemTypeNamePlural: "requests")
{
    private StraumrWorkspaceEntry? _workspaceEntry;
    private string? _workspaceDir;

    protected override string ModelHintsText => "s Set active  c Create  d Delete  e Edit  y Copy  I Import  x Export";

    protected override void OnInitialized(IReadOnlyList<RequestEntry> entries)
    {
        int requestCount = optionsService.Options.Workspaces.Count;
        ShowSuccess($"{requestCount} request{(requestCount == 1 ? string.Empty : "s")} loaded");
    }

    protected override string GetDisplayText(RequestEntry entry) => entry.Display;

    protected override bool IsSameEntry(RequestEntry? left, RequestEntry? right)
        => left?.Id == right?.Id;

    protected override bool HandleModelKeyDown(Key key, RequestEntry? selectedEntry)
    {
        if (key is { IsCtrl: false, IsAlt: false })
        {
            switch (KeyHelpers.GetCharValue(key))
            {
                case 's':
                    SetCurrentWorkspace(selectedEntry);
                    return true;
                case 'c':
                    CreateRequest();
                    return true;
                case 'd':
                    DeleteRequest(selectedEntry);
                    return true;
                case 'e':
                    EditRequest(selectedEntry);
                    return true;
                case 'y':
                    CopyRequest(selectedEntry);
                    return true;
            }
        }

        return false;
    }

    private void SetCurrentWorkspace(RequestEntry? selectedItem)
    {
        if (selectedItem is null)
        {
            return;
        }

        if (selectedItem.IsDamaged)
        {
            ShowDanger($"😬 {selectedItem.Identifier} is damaged and cannot be set as default workspace.");
            return;
        }

        RefreshAsync().GetAwaiter().GetResult();
        ShowSuccess($"😎 {selectedItem.Identifier} is now the active workspace.");
    }

    private void CreateRequest() { }

    private void DeleteRequest(RequestEntry? selectedEntry)
    {
        if (selectedEntry is null)
        {
            return;
        }

        string? confirm = interactiveConsole.Select(
            $"Delete \"{selectedEntry.Identifier}\"?",
            ["Cancel", "Delete"],
            enableFilter: false,
            enableTypeahead: true);

        if (confirm is not "Delete")
        {
            return;
        }

        try
        {
            _ = RefreshAsync();
            ShowSuccess($" Deleted workspace \"{selectedEntry.Identifier}\".");
        }
        catch (StraumrException ex)
        {
            ShowDanger($" {ex.Message}");
        }
        catch (Exception ex)
        {
            ShowDanger($" {ex.Message}");
        }
    }

    private void EditRequest(RequestEntry? selectedEntry)
    {

    }

    private void CopyRequest(RequestEntry? selectedEntry)
    {

    }

    protected override IEnumerable<ModelCommand> GetCommands()
    {
        yield return new ModelCommand("set", "Set selected workspace as active",
            _ => SetCurrentWorkspace(SelectedEntry), "use");
        yield return new ModelCommand("create", "Create a new workspace", _ => CreateRequest(), "new");
        yield return new ModelCommand("delete", "Delete selected workspace", _ => DeleteRequest(SelectedEntry), "rm",
            "remove");
        yield return new ModelCommand("edit", "Edit selected workspace in $EDITOR", _ => EditRequest(SelectedEntry));
        yield return new ModelCommand("copy", "Copy selected workspace to a new name", _ => CopyRequest(SelectedEntry),
            "cp");
    }

    protected override void OpenSelectedEntry() { }

    protected override void InspectSelectedEntry()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        interactiveConsole.ShowDetails(
            string.Empty,
            [
                ("ID", $"[bold]{SelectedEntry.Id}[/]"),
                ("Name", SelectedEntry.Name is not null ? $"{SelectedEntry.Identifier}" : "[secondary]N/A[/]"),
                ("Method", SelectedEntry.Method?.ToString() ?? "[secondary]N/A[/]"),
                ("Host", SelectedEntry.ShortUriHostname ?? "[secondary]N/A[/]"),
                ("Body", SelectedEntry.BodyTypeText ?? "[secondary]N/A[/]"),
                ("Auth", SelectedEntry.Auth ?? "[secondary]N/A[/]"),
                ("Last Accessed",
                    SelectedEntry.LastAccessed?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[warning]N/A[/]"),
                ("Status", SelectedEntry.IsDamaged ? "[danger][bold]Damaged[/][/]" : "[success][bold]Valid[/][/]")
            ]);
    }

    protected override async Task<IReadOnlyList<RequestEntry>> LoadEntriesAsync(CancellationToken cancellationToken)
    {
        _workspaceEntry ??= navigationContext.GetWorkspaceEntry();
        if (_workspaceEntry is null)
        {
            interactiveConsole.ShowMessage("No workspace selected.",
                "You will now be navigated to workspaces menu. Set an active workspace to load underlying entities");
            NavigateTo<WorkspacesScreen>();
            return [];
        }

        _workspaceDir = Path.GetDirectoryName(_workspaceEntry.Path);

        StraumrWorkspace workspace;
        try
        {
            workspace = await workspaceService.GetWorkspace(_workspaceEntry.Path);
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            interactiveConsole.ShowMessage("Workspace corrupt",
                "Your selected workspace is corrupt. Your active workspace will be cleared and you will be navigated to the workspaces menu. " +
                "From there you can attempt to rescue your workspace using the edit tool.");
            NavigateTo<WorkspacesScreen>();
            return [];
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.MissingEntry)
        {
            interactiveConsole.ShowMessage("Workspace missing",
                "Your selected workspace is missing from disk. Your active workspace will be cleared and you will be navigated to the workspaces menu.");
            NavigateTo<WorkspacesScreen>();
            return [];
        }

        var items = new List<RequestEntry>();
        foreach (Guid requestId in workspace.Requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.Add(await BuildRequestEntryAsync(requestId));
        }

        return items
            .OrderBy(item => item.Name is null ? 1 : 0)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .ToList();
    }

    private async Task<RequestEntry> BuildRequestEntryAsync(Guid requestId)
    {
        string identifier = requestId.ToString();
        string? name = null;
        string status = "Valid";
        bool isDamaged = false;
        HttpMethod? method = null;
        string? shortUri = null;
        string? bodyTypeText = null;
        string? authText = null;
        DateTimeOffset? lastAccessed = null;
        string display;

        try
        {
            string requestPath = Path.Combine(_workspaceDir!, $"{requestId}.json");
            if (!File.Exists(requestPath))
            {
                throw new StraumrException("Request file missing", StraumrError.MissingEntry);
            }

            StraumrRequest request = await requestService.GetAsync(requestId.ToString(), _workspaceEntry);
            identifier = request.Name;
            name = request.Name;
            method = request.Method;
            Uri.TryCreate(request.Uri, UriKind.Absolute, out Uri? uri);
            shortUri = uri?.Host ?? request.Uri;
            bodyTypeText = request.BodyType == BodyType.None ? "No body" : request.BodyType.ToString();
            authText = await GetAuthText(request.AuthId?.ToString());
            lastAccessed = request.LastAccessed;

            string line0 = $"[accent]◇ {request.Method}[/] [bold]{request.Name}[/]";
            string line1 = $"  [secondary]{request.Id}[/]";
            string statsRight =
                $"{shortUri} · {bodyTypeText} · {authText}  [/][info]{request.LastAccessed.LocalDateTime:yyyy-MM-dd}[/]";
            string line2 = $"  [secondary]{statsRight}";
            display = $"{line0}\n{line1}\n{line2}";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            isDamaged = true;
            status = "Corrupt";
            display = $"[danger]✖[/] [bold]{requestId}[/]  [danger](Corrupt)[/]\n  [danger]Request file is corrupt[/]";
        }
        catch (StraumrException ex) when (ex.Reason is StraumrError.MissingEntry or StraumrError.EntryNotFound)
        {
            isDamaged = true;
            status = "Missing";
            display = $"[danger]✖[/] [bold]{requestId}[/]  [warning](Missing)[/]\n  [warning]Request file is missing[/]";
        }

        return new RequestEntry
        {
            Id = requestId,
            Display = display,
            Identifier = identifier,
            Status = status,
            IsDamaged = isDamaged,
            Method = method,
            ShortUriHostname = shortUri,
            BodyTypeText = bodyTypeText,
            Auth = authText,
            LastAccessed = lastAccessed,
            Name = name,
        };
    }

    private async Task<string> GetAuthText(string? authId)
    {
        var authText = "No auth";
        if (authId == null) return authText;

        try
        {
            StraumrAuth auth = await authService.GetAsync(authId);
            return $"{auth.Name}({auth.Config.Type})";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            authText = "Corrupted auth";
        }
        catch (StraumrException ex) when (ex.Reason is StraumrError.MissingEntry or StraumrError.EntryNotFound)
        {
            authText = "Missing auth";
        }
        catch (Exception ex)
        {
            authText = "Failed to read auth";
        }

        return authText;
    }
}