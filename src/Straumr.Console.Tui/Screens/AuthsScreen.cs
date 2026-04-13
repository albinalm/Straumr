using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Console;
using Straumr.Console.Tui.Helpers;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Models;
using Straumr.Console.Tui.Screens.Base;
using Straumr.Console.Tui.Services.Interfaces;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens;

public sealed class AuthsScreen(
    TuiInteractiveConsole interactiveConsole,
    IStraumrWorkspaceService workspaceService,
    IStraumrAuthService authService,
    ScreenNavigationContext navigationContext,
    IAuthEditor authEditor,
    IWorkspaceGuard workspaceGuard,
    ITuiOperationExecutor operationExecutor,
    StraumrTheme theme)
    : ModelScreen<AuthEntry>(theme,
        screenTitle: "Auths",
        emptyStateText: "No auths found",
        itemTypeNamePlural: "auths")
{
    private readonly IAuthEditor _authEditor = authEditor;
    private readonly IWorkspaceGuard _workspaceGuard = workspaceGuard;
    private readonly ITuiOperationExecutor _executor = operationExecutor;
    private StraumrWorkspaceEntry? _workspaceEntry;
    private bool _editorActive;

    protected override string ModelHintsText => "c Create  d Delete  e Edit  y Copy";

    protected override void OnInitialized()
    {
        _workspaceEntry ??= navigationContext.GetWorkspaceEntry();
        if (_workspaceEntry is null)
        {
            ShowInfo("No workspace selected. Use the workspaces screen to activate one");
            return;
        }

        try
        {
            StraumrWorkspace workspace =
                workspaceService.PeekWorkspace(_workspaceEntry.Path).GetAwaiter().GetResult();
            int authCount = workspace.Auths.Count;
            ShowSuccess($"{authCount} auth{(authCount == 1 ? string.Empty : "s")} loaded");
        }
        catch (StraumrException ex)
        {
            ShowDanger(ex.Message);
        }
        catch (Exception ex)
        {
            ShowDanger(ex.Message);
        }
    }

    protected override string GetDisplayText(AuthEntry entry) => entry.Display;

    protected override bool IsSameEntry(AuthEntry? left, AuthEntry? right)
        => left?.Id == right?.Id;

    protected override bool HandleModelKeyDown(Key key, AuthEntry? selectedEntry)
    {
        if (_editorActive)
        {
            return true;
        }

        if (key is { IsCtrl: false, IsAlt: false })
        {
            switch (KeyHelpers.GetCharValue(key))
            {
                case 'c':
                    CreateAuth();
                    return true;
                case 'd':
                    DeleteAuth(selectedEntry);
                    return true;
                case 'e':
                    EditAuth(selectedEntry);
                    return true;
                case 'y':
                    CopyAuth(selectedEntry);
                    return true;
            }
        }

        return false;
    }

    private void CreateAuth()
    {
        if (_editorActive)
        {
            return;
        }

        if (!TryResolveWorkspace(out StraumrWorkspaceEntry workspaceEntry))
        {
            return;
        }

        RunEditor(AuthEditorMode.Create, workspaceEntry, null);
    }

    private void EditAuth(AuthEntry? selectedEntry)
    {
        if (selectedEntry is null || selectedEntry.IsDamaged)
        {
            return;
        }

        if (_editorActive)
        {
            return;
        }

        if (!TryResolveWorkspace(out StraumrWorkspaceEntry workspaceEntry))
        {
            return;
        }

        if (!_executor.TryExecute(
                () => authService.GetAsync(selectedEntry.Id.ToString(), workspaceEntry).GetAwaiter().GetResult(),
                ShowDanger,
                out StraumrAuth? auth) || auth is null)
        {
            return;
        }

        RunEditor(AuthEditorMode.Edit, workspaceEntry, auth);
    }

    private void DeleteAuth(AuthEntry? selectedEntry)
    {
        if (selectedEntry is null)
        {
            return;
        }

        if (!TryResolveWorkspace(out StraumrWorkspaceEntry workspaceEntry))
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

        if (!_executor.TryExecute(
                () => authService.DeleteAsync(selectedEntry.Id.ToString(), workspaceEntry).GetAwaiter().GetResult(),
                ShowDanger))
        {
            return;
        }

        _ = RefreshAsync();
        ShowSuccess($"Deleted auth \"{selectedEntry.Identifier}\"");
    }

    private void CopyAuth(AuthEntry? selectedEntry)
    {
        if (selectedEntry is null)
        {
            return;
        }

        if (selectedEntry.IsDamaged)
        {
            ShowDanger($"Cannot copy damaged auth \"{selectedEntry.Identifier}\"");
            return;
        }

        if (!TryResolveWorkspace(out StraumrWorkspaceEntry workspaceEntry))
        {
            return;
        }

        string? newName = interactiveConsole.TextInput(
            "New name",
            selectedEntry.Name ?? selectedEntry.Identifier,
            validate: value => string.IsNullOrWhiteSpace(value) ? "Name cannot be empty." : null);
        if (newName is null)
        {
            return;
        }

        if (!_executor.TryExecute(
                () => authService.CopyAsync(selectedEntry.Id.ToString(), newName, workspaceEntry).GetAwaiter()
                    .GetResult(),
                ShowDanger))
        {
            return;
        }

        _ = RefreshAsync();
        ShowSuccess($"Copied auth to \"{newName}\"");
    }

    protected override void OpenSelectedEntry() { }

    protected override void InspectSelectedEntry()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        StraumrAuth? auth = null;
        if (TryResolveWorkspace(out StraumrWorkspaceEntry workspaceEntry) && !SelectedEntry.IsDamaged)
        {
            try
            {
                auth = authService.GetAsync(SelectedEntry.Id.ToString(), workspaceEntry).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        string typeDisplay = auth is not null ? AuthDisplayFormatter.GetAuthTypeName(auth.Config) : "[secondary]N/A[/]";
        interactiveConsole.ShowDetails(
            string.Empty,
            [
                ("ID", $"[bold]{SelectedEntry.Id}[/]"),
                ("Name", SelectedEntry.Name is not null ? $"{SelectedEntry.Identifier}" : "[secondary]N/A[/]"),
                ("Type", typeDisplay),
                ("Auto Renew", SelectedEntry.AutoRenew ? "[success]Enabled[/]" : "[secondary]Disabled[/]"),
                ("Modified",
                    SelectedEntry.Modified?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[secondary]N/A[/]"),
                ("Last Accessed",
                    SelectedEntry.LastAccessed?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[secondary]N/A[/]"),
                ("Status", SelectedEntry.IsDamaged ? "[danger][bold]Damaged[/][/]" : "[success][bold]Valid[/][/]")
            ]);
    }

    protected override IEnumerable<ModelCommand> GetCommands()
    {
        yield return new ModelCommand("create", _ => CreateAuth(), "new");
        yield return new ModelCommand("delete", _ => DeleteAuth(SelectedEntry), "rm", "remove");
        yield return new ModelCommand("edit", _ => EditAuth(SelectedEntry));
        yield return new ModelCommand("copy", _ => CopyAuth(SelectedEntry), "cp");
    }

    protected override async Task<IReadOnlyList<AuthEntry>> LoadEntriesAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveWorkspace(out StraumrWorkspaceEntry workspaceEntry))
        {
            return [];
        }

        StraumrWorkspace workspace;
        try
        {
            workspace = await workspaceService.GetWorkspace(workspaceEntry.Path);
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            interactiveConsole.ShowMessage("Workspace corrupt",
                "Your selected workspace is corrupt. Your active workspace will be cleared and you will be navigated to the workspaces menu.");
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

        string workspaceName = string.IsNullOrWhiteSpace(workspace.Name)
            ? workspaceEntry.Id.ToString()
            : workspace.Name;
        UpdateTitle($"Auths - {workspaceName}");

        var entries = new List<AuthEntry>();
        foreach (Guid authId in workspace.Auths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(await BuildAuthEntryAsync(authId));
        }

        return entries
            .OrderBy(item => item.Name is null ? 1 : 0)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .ToList();
    }

    private async Task<AuthEntry> BuildAuthEntryAsync(Guid authId)
    {
        string identifier = authId.ToString();
        string status = "Valid";
        bool isDamaged = false;
        string? type = null;
        bool autoRenew = false;
        DateTimeOffset? lastAccessed = null;
        DateTimeOffset? modified = null;
        string? name = null;
        string display;

        try
        {
            StraumrAuth auth = await authService.PeekByIdAsync(authId, _workspaceEntry);
            identifier = auth.Name;
            name = auth.Name;
            type = AuthDisplayFormatter.GetAuthTypeName(auth.Config);
            autoRenew = auth.AutoRenewAuth;
            lastAccessed = auth.LastAccessed;
            modified = auth.Modified;

            string state = autoRenew ? "Auto renew" : "Manual";
            string line0 = $"[accent]◇ {type}[/] [bold]{auth.Name}[/]";
            string line1 = $"  [secondary]{auth.Id}[/]";
            string line2 = $"  [secondary]{state}  [/][info]{auth.Modified.LocalDateTime:yyyy-MM-dd}[/]";
            display = $"{line0}\n{line1}\n{line2}";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            isDamaged = true;
            status = "Corrupt";
            display = $"[danger]X[/] [bold]{authId}[/]  [danger](Corrupt)[/]\n  [danger]Auth file is corrupt[/]";
        }
        catch (StraumrException ex) when (ex.Reason is StraumrError.MissingEntry or StraumrError.EntryNotFound)
        {
            isDamaged = true;
            status = "Missing";
            display = $"[danger]X[/] [bold]{authId}[/]  [warning](Missing)[/]\n  [warning]Auth file is missing[/]";
        }

        return new AuthEntry
        {
            Id = authId,
            Display = display,
            Identifier = identifier,
            Status = status,
            IsDamaged = isDamaged,
            Type = type,
            AutoRenew = autoRenew,
            LastAccessed = lastAccessed,
            Modified = modified,
            Name = name
        };
    }

    private bool TryResolveWorkspace(out StraumrWorkspaceEntry workspaceEntry)
    {
        WorkspaceGuardResult result = _workspaceGuard.EnsureActiveWorkspace();
        if (!result.HasWorkspace || result.WorkspaceEntry is null)
        {
            NavigateTo<WorkspacesScreen>();
            workspaceEntry = null!;
            return false;
        }

        _workspaceEntry = result.WorkspaceEntry;
        workspaceEntry = result.WorkspaceEntry;
        return true;
    }

    private void RunEditor(AuthEditorMode mode, StraumrWorkspaceEntry workspaceEntry, StraumrAuth? existingAuth)
    {
        if (_editorActive)
        {
            return;
        }

        _editorActive = true;
        try
        {
            var context = new AuthEditorContext(
                mode,
                workspaceEntry,
                existingAuth,
                () => RefreshAsync(),
                ShowSuccess,
                ShowDanger);
            _authEditor.Run(context);
        }
        finally
        {
            _editorActive = false;
            FocusList();
        }
    }
}