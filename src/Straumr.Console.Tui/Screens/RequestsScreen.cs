using System.Diagnostics;
using System.Text;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Console;
using Straumr.Console.Tui.Helpers;
using Straumr.Console.Tui.Components.Prompts.Form;
using Straumr.Console.Tui.Models;
using Straumr.Console.Tui.Screens.Base;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens;

public sealed class RequestsScreen(
    TuiInteractiveConsole interactiveConsole,
    IStraumrRequestService requestService,
    IStraumrOptionsService optionsService,
    StraumrTheme theme)
    : ModelScreen<RequestEntry>(theme,
        screenTitle: "Requests",
        emptyStateText: "No requests found",
        itemTypeNamePlural: "requests")
{
    protected override string ModelHintsText => "s Set active  c Create  d Delete  e Edit  y Copy  I Import  x Export";

    protected override void OnInitialized(IReadOnlyList<RequestEntry> entries)
    {
        int requestCount = optionsService.Options.Workspaces.Count;
        ShowSuccess($"{requestCount} request{(requestCount == 1 ? string.Empty : "s")} loaded");
    }

    protected override string GetDisplayText(RequestEntry entry) => entry.Display;

    protected override bool IsSameEntry(RequestEntry? left, RequestEntry? right)
        => left?.StraumrEntry.Id == right?.StraumrEntry.Id;

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
        if (selectedEntry is null)
        {
            return;
        }

        if (selectedEntry.IsDamaged)
        {
            ShowDanger($" Cannot edit damaged workspace \"{selectedEntry.Identifier}\".");
            return;
        }

        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (string.IsNullOrWhiteSpace(editor))
        {
            ShowDanger(" $EDITOR is not set.");
            return;
        }

        string identifier = selectedEntry.Identifier;
        RequestExternalAndRefresh(async () =>
        {
            string tempPath;
            try
            {
                tempPath = "";
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
            ShowDanger($" Cannot copy damaged workspace \"{selectedEntry.Identifier}\".");
            return;
        }

        List<FormFieldSpec> fields =
        [
            new("name", "New name", Required: true),
            new("outputDir", "Output directory"),
        ];

        Dictionary<string, string>? result = interactiveConsole.PromptForm("Copy workspace", fields);
        if (result is null)
        {
            return;
        }

        string newName = result["name"];
        string? outputDir = result.TryGetValue("outputDir", out string? value) ? value : null;
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            outputDir = null;
        }

        try
        {
            _ = RefreshAsync();
            ShowSuccess($" Copied workspace to \"{newName}\".");
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
                ("ID", $"[bold]{SelectedEntry.StraumrEntry.Id}[/]"),
                ("Name", SelectedEntry.Name is not null ? $"{SelectedEntry.Identifier}" : "[secondary]N/A[/]"),
                ("Requests",
                    SelectedEntry.RequestCount is not null ? $"{SelectedEntry.RequestCount}" : "[secondary]N/A[/]"),
                ("Secrets",
                    SelectedEntry.SecretCount is not null ? $"{SelectedEntry.SecretCount}" : "[secondary]N/A[/]"),
                ("Authenticators",
                    SelectedEntry.AuthCount is not null ? $"{SelectedEntry.AuthCount}" : "[secondary]N/A[/]"),
                ("Path", SelectedEntry.StraumrEntry.Path),
                ("Last Accessed",
                    SelectedEntry.LastAccessed?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[warning]N/A[/]"),
                ("Status", SelectedEntry.IsDamaged ? "[danger][bold]Damaged[/][/]" : "[success][bold]Valid[/][/]")
            ]);
    }

    protected override async Task<IReadOnlyList<RequestEntry>> LoadEntriesAsync(CancellationToken cancellationToken)
    {
        if (optionsService.Options.CurrentWorkspace is null)
        {
             interactiveConsole.ShowMessage("No workspace selected.",
                "You will now be navigated to workspaces menu. Set an active workspace to load underlying entities");
             NavigateTo<WorkspacesScreen>();
        }

        return [];
    }
}