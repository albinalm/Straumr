using System.Diagnostics;
using System.IO;
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

public sealed class RequestsScreen(
    TuiInteractiveConsole interactiveConsole,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService,
    IStraumrAuthService authService,
    IStraumrOptionsService optionsService,
    ScreenNavigationContext navigationContext,
    IRequestEditor requestEditor,
    IWorkspaceGuard workspaceGuard,
    ITuiOperationExecutor operationExecutor,
    StraumrTheme theme)
    : ModelScreen<RequestEntry>(theme,
        screenTitle: "Requests",
        emptyStateText: "No requests found",
        itemTypeNamePlural: "requests")
{
    private StraumrWorkspaceEntry? _workspaceEntry;
    private string? _workspaceDir;
    private bool _editorActive;

    protected override string ModelHintsText => $"s Send  c Create  d Delete  e Edit  E Edit with " +
                                                $"{Path.GetFileNameWithoutExtension(Environment.GetEnvironmentVariable("EDITOR")) ?? "Undefined"}  y Copy";

    protected override void OnInitialized()
    {
        int requestCount = optionsService.Options.Workspaces.Count;
        ShowSuccess($"{requestCount} request{(requestCount == 1 ? string.Empty : "s")} loaded");
    }

    protected override string GetDisplayText(RequestEntry entry) => entry.Display;

    protected override bool IsSameEntry(RequestEntry? left, RequestEntry? right)
        => left?.Id == right?.Id;

    protected override bool HandleModelKeyDown(Key key, RequestEntry? selectedEntry)
    {
        if (_editorActive)
        {
            return true;
        }

        if (key is { IsCtrl: false, IsAlt: false })
        {
            switch (KeyHelpers.GetCharValue(key))
            {
                case 's':
                    SendRequest(selectedEntry);
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
                case 'E':
                    EditRequestWithEditor(selectedEntry);
                    return true;
                case 'y':
                    CopyRequest(selectedEntry);
                    return true;
            }
        }

        return false;
    }

    private void SendRequest(RequestEntry? selectedEntry)
    {
        if (selectedEntry is null)
        {
            return;
        }

        if (selectedEntry.IsDamaged)
        {
            ShowDanger($"Cannot send damaged request \"{selectedEntry.Identifier}\"");
            return;
        }

        if (!TryResolveWorkspace(out StraumrWorkspaceEntry workspaceEntry))
        {
            return;
        }

        navigationContext.SetWorkspace(workspaceEntry);
        navigationContext.SetRequest(selectedEntry.Id);
        NavigateTo<SendScreen>();
    }

    private void CreateRequest()
    {
        if (_editorActive)
        {
            return;
        }

        if (!TryResolveWorkspace(out StraumrWorkspaceEntry workspaceEntry))
        {
            return;
        }

        RunEditor(RequestEditorMode.Create, workspaceEntry, null);
    }

    private void DeleteRequest(RequestEntry? selectedEntry)
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

        if (!operationExecutor.TryExecute(
                () => requestService.DeleteAsync(selectedEntry.Id.ToString(), workspaceEntry).GetAwaiter().GetResult(),
                ShowDanger))
        {
            return;
        }

        _ = RefreshAsync();
        ShowSuccess($"Deleted request \"{selectedEntry.Identifier}\"");
    }

    private void EditRequest(RequestEntry? selectedEntry)
    {
        if (selectedEntry is null)
        {
            return;
        }

        if (selectedEntry.IsDamaged)
        {
            EditRequestWithEditor(selectedEntry);
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

        if (!operationExecutor.TryExecute(
                () => requestService.GetAsync(selectedEntry.Id.ToString(), workspaceEntry).GetAwaiter().GetResult(),
                ShowDanger,
                out StraumrRequest? request) || request is null)
        {
            return;
        }

        RunEditor(RequestEditorMode.Edit, workspaceEntry, request);
    }

    private void EditRequestWithEditor(RequestEntry? selectedEntry)
    {
        if (selectedEntry is null)
        {
            return;
        }

        if (!TryResolveWorkspace(out StraumrWorkspaceEntry workspaceEntry))
        {
            return;
        }

        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (string.IsNullOrWhiteSpace(editor))
        {
            ShowDanger("$EDITOR is not set");
            return;
        }

        string identifier = selectedEntry.Identifier;
        RequestExternalAndRefresh(async () =>
        {
            Guid requestId;
            string tempPath;
            try
            {
                (requestId, tempPath) = await requestService.PrepareEditAsync(identifier, workspaceEntry);
            }
            catch
            {
                return;
            }

            try
            {
                Process? process = Process.Start(new ProcessStartInfo(editor, tempPath)
                {
                    UseShellExecute = false,
                });

                if (process is null)
                {
                    return;
                }

                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    requestService.ApplyEdit(requestId, tempPath, workspaceEntry);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        });
    }

    private void CopyRequest(RequestEntry? selectedEntry)
    {
        if (selectedEntry is null)
        {
            return;
        }

        if (selectedEntry.IsDamaged)
        {
            ShowDanger($"Cannot copy damaged request \"{selectedEntry.Identifier}\"");
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

        if (!operationExecutor.TryExecute(
                () => requestService.CopyAsync(selectedEntry.Id.ToString(), newName, workspaceEntry).GetAwaiter()
                    .GetResult(),
                ShowDanger))
        {
            return;
        }

        _ = RefreshAsync();
        ShowSuccess($"Copied request to \"{newName}\"");
    }

    protected override IEnumerable<ModelCommand> GetCommands()
    {
        yield return new ModelCommand("send", _ => SendRequest(SelectedEntry), "run");
        yield return new ModelCommand("create", _ => CreateRequest(), "new");
        yield return new ModelCommand("delete", _ => DeleteRequest(SelectedEntry), "rm", "remove");
        yield return new ModelCommand("edit", _ => EditRequest(SelectedEntry));
        yield return new ModelCommand("editor", _ => EditRequestWithEditor(SelectedEntry));
        yield return new ModelCommand("copy", _ => CopyRequest(SelectedEntry), "cp");
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

    private bool TryResolveWorkspace(out StraumrWorkspaceEntry workspaceEntry)
    {
        WorkspaceGuardResult result = workspaceGuard.EnsureActiveWorkspace();
        if (!result.HasWorkspace || result.WorkspaceEntry is null)
        {
            NavigateTo<WorkspacesScreen>();
            workspaceEntry = null!;
            return false;
        }

        _workspaceEntry = result.WorkspaceEntry;
        _workspaceDir = Path.GetDirectoryName(result.WorkspaceEntry.Path);
        workspaceEntry = result.WorkspaceEntry;
        return true;
    }

    private void RunEditor(
        RequestEditorMode mode,
        StraumrWorkspaceEntry workspaceEntry,
        StraumrRequest? existingRequest)
    {
        if (_editorActive)
        {
            return;
        }

        _editorActive = true;
        try
        {
            var context = new RequestEditorContext(
                mode,
                workspaceEntry,
                existingRequest,
                () => RefreshAsync(),
                ShowSuccess,
                ShowDanger);
            requestEditor.Run(context);
        }
        finally
        {
            _editorActive = false;
            FocusList();
        }
    }

    protected override async Task<IReadOnlyList<RequestEntry>> LoadEntriesAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveWorkspace(out StraumrWorkspaceEntry workspaceEntry))
        {
            return [];
        }

        StraumrWorkspace workspace;
        try
        {
            workspace = await workspaceService.GetWorkspaceAsync(workspaceEntry.Path);
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

        string workspaceName = string.IsNullOrWhiteSpace(workspace.Name)
            ? workspaceEntry.Id.ToString()
            : workspace.Name;
        UpdateTitle($"Requests - {workspaceName}");

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

            StraumrRequest request = await requestService.GetAsync(requestId.ToString(), _workspaceEntry!);
            identifier = request.Name;
            name = request.Name;
            method = request.Method;
            Uri.TryCreate(request.Uri, UriKind.Absolute, out Uri? uri);
            shortUri = uri?.Host ?? request.Uri;
            bodyTypeText = request.BodyType == BodyType.None ? "No body" : request.BodyType.ToString();
            authText = await GetAuthText(request.AuthId?.ToString());
            lastAccessed = request.LastAccessed;

            string methodTag = HttpMethodMarkup.TagFor(request.Method);
            var line0 = $"[{methodTag}]◇ {request.Method}[/] [bold]{request.Name}[/]";
            var line1 = $"  [secondary]{request.Id}[/]";
            var statsRight =
                $"{shortUri} · {bodyTypeText} · {authText}  [/][info]{request.LastAccessed.LocalDateTime:yyyy-MM-dd}[/]";
            var line2 = $"  [secondary]{statsRight}";
            display = $"{line0}\n{line1}\n{line2}";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            isDamaged = true;
            status = "Corrupt";
            display = $"[danger]X[/] [bold]{requestId}[/]  [danger](Corrupt)[/]\n  [danger]Request file is corrupt[/]";
        }
        catch (StraumrException ex) when (ex.Reason is StraumrError.MissingEntry or StraumrError.EntryNotFound)
        {
            isDamaged = true;
            status = "Missing";
            display = $"[danger]X[/] [bold]{requestId}[/]  [warning](Missing)[/]\n  [warning]Request file is missing[/]";
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
            StraumrAuth auth = await authService.GetAsync(authId, _workspaceEntry!);
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
        catch (Exception)
        {
            authText = "Failed to read auth";
        }

        return authText;
    }
}
