using System.Diagnostics;
using System.Text;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Console;
using Straumr.Console.Tui.Components.Prompts.Form;
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
    private const string WorkspaceHintsText = "j/k Navigate  g/G Jump  s Set active  c Create  d Delete  e Edit  y Copy  I Import  x Export  i Inspect  / Filter  : Command";

    protected override void OnInitialized(IReadOnlyList<WorkspaceEntry> entries)
    {
        int workspaceCount = optionsService.Options.Workspaces.Count;
        ShowSuccess($" {workspaceCount} workspace{(workspaceCount == 1 ? string.Empty : "s")} loaded");
    }

    protected override string GetDisplayText(WorkspaceEntry entry) => entry.Display;

    protected override bool IsSameEntry(WorkspaceEntry? left, WorkspaceEntry? right)
        => left?.StraumrEntry.Id == right?.StraumrEntry.Id;

    protected override bool HandleModelKeyDown(Key key, WorkspaceEntry? selectedEntry)
    {
        Rune rune = key.AsRune;
        if (key is { IsCtrl: false, IsAlt: false })
        {
            switch (rune.Value)
            {
                case 's':
                    SetCurrentWorkspace(selectedEntry);
                    return true;
                case 'c':
                    CreateWorkspace();
                    return true;
                case 'd':
                    DeleteWorkspace(selectedEntry);
                    return true;
                case 'e':
                    EditWorkspace(selectedEntry);
                    return true;
                case 'y':
                    CopyWorkspace(selectedEntry);
                    return true;
                case 'I':
                    ImportWorkspace();
                    return true;
                case 'x':
                    ExportWorkspace(selectedEntry);
                    return true;
            }
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
        RefreshAsync().GetAwaiter().GetResult();
        ShowSuccess($"😎 {selectedItem.Identifier} is now the active workspace.");
    }

    private void CreateWorkspace()
    {
        List<FormFieldSpec> fields =
        [
            new("name", "Name", Required: true),
            new("outputDir", "Output directory"),
        ];

        Dictionary<string, string>? result = interactiveConsole.PromptForm("Create workspace", fields);
        if (result is null)
        {
            return;
        }

        string name = result["name"];
        string? outputDir = result.TryGetValue("outputDir", out string? value) ? value : null;
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            outputDir = null;
        }

        try
        {
            StraumrWorkspace workspace = new() { Name = name };
            workspaceService.Create(workspace, outputDir).GetAwaiter().GetResult();
            _ = RefreshAsync();
            ShowSuccess($" Created workspace \"{name}\".");
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

    private void DeleteWorkspace(WorkspaceEntry? selectedEntry)
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
            workspaceService.Delete(selectedEntry.Identifier).GetAwaiter().GetResult();
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

    private void EditWorkspace(WorkspaceEntry? selectedEntry)
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
                tempPath = await workspaceService.PrepareEdit(identifier);
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
                    await workspaceService.ApplyEdit(identifier, tempPath);
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

    private void CopyWorkspace(WorkspaceEntry? selectedEntry)
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
            workspaceService.Copy(selectedEntry.Identifier, newName, outputDir).GetAwaiter().GetResult();
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

    private void ImportWorkspace()
    {
        string? path = interactiveConsole.TextInput("Path to workspace file",
            validate: v =>
            {
                if (string.IsNullOrWhiteSpace(v))
                {
                    return "Path cannot be empty.";
                }

                if (!File.Exists(v))
                {
                    return "File not found.";
                }

                return null;
            });

        if (path is null)
        {
            return;
        }

        try
        {
            workspaceService.Import(path).GetAwaiter().GetResult();
            _ = RefreshAsync();
            ShowSuccess($" Imported workspace from \"{path}\".");
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

    private void ExportWorkspace(WorkspaceEntry? selectedEntry)
    {
        if (selectedEntry is null)
        {
            return;
        }

        if (selectedEntry.IsDamaged)
        {
            ShowDanger($" Cannot export damaged workspace \"{selectedEntry.Identifier}\".");
            return;
        }

        string? outputDir = interactiveConsole.TextInput("Output directory",
            validate: v => string.IsNullOrWhiteSpace(v) ? "Output directory cannot be empty." : null);

        if (outputDir is null)
        {
            return;
        }

        try
        {
            string outputFile = workspaceService.Export(selectedEntry.Identifier, outputDir).GetAwaiter().GetResult();
            ShowSuccess($" Exported workspace to \"{outputFile}\".");
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
        yield return new ModelCommand("set", "Set selected workspace as active", _ => SetCurrentWorkspace(SelectedEntry), "use");
        yield return new ModelCommand("create", "Create a new workspace", _ => CreateWorkspace(), "new");
        yield return new ModelCommand("delete", "Delete selected workspace", _ => DeleteWorkspace(SelectedEntry), "rm", "remove");
        yield return new ModelCommand("edit", "Edit selected workspace in $EDITOR", _ => EditWorkspace(SelectedEntry));
        yield return new ModelCommand("copy", "Copy selected workspace to a new name", _ => CopyWorkspace(SelectedEntry), "cp");
        yield return new ModelCommand("import", "Import a workspace from a file", _ => ImportWorkspace());
        yield return new ModelCommand("export", "Export selected workspace to a file", _ => ExportWorkspace(SelectedEntry));
    }

    protected override void OpenSelectedEntry()
    {
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
            .OrderBy(item => item.Name is null ? 1 : 0)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.StraumrEntry.Id)
            .ToList();
    }

    private async Task<WorkspaceEntry> BuildWorkspaceEntryAsync(StraumrWorkspaceEntry entry)
    {
        DateTimeOffset? lastAccessed = null;
        bool isDamaged = false;
        string identifier = entry.Id.ToString();
        string? name = null;
        string status = "Valid";
        int? requests = null;
        int? secrets = null;
        int? auths = null;
        string display;

        bool isCurrent = optionsService.Options.CurrentWorkspace?.Id == entry.Id;

        try
        {
            StraumrWorkspace workspace = await workspaceService.PeekWorkspace(entry.Path);
            lastAccessed = workspace.LastAccessed;
            identifier = workspace.Name;
            requests = workspace.Requests.Count;
            secrets = workspace.Secrets.Count;
            auths = workspace.Auths.Count;
            name = workspace.Name;

            string line0 = isCurrent
                ? $"[accent]▸[/] [bold]{workspace.Name}[/]  [accent](current)[/]"
                : $"[secondary]◇[/] [bold]{workspace.Name}[/]";

            string line1 = $"  [secondary]{entry.Id}[/]";

            string statsRight = lastAccessed.HasValue
                ? $"{requests} req · {secrets} sec · {auths} auth  [/][info]{lastAccessed.Value.LocalDateTime:yyyy-MM-dd}[/]"
                : $"{requests} req · {secrets} sec · {auths} auth[/]";
            string line2 = $"  [secondary]{statsRight}";

            display = $"{line0}\n{line1}\n{line2}";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            isDamaged = true;
            status = "Corrupt";
            display = $"[danger]✖[/] [bold]{entry.Id}[/]  [danger](Corrupt)[/]\n  [secondary]{entry.Path}[/]\n  [danger]Workspace file is corrupt[/]";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.EntryNotFound)
        {
            isDamaged = true;
            status = "Missing";
            display = $"[danger]✖[/] [bold]{entry.Id}[/]  [warning](Missing)[/]\n  [secondary]{entry.Path}[/]\n  [warning]Workspace file is missing[/]";
        }

        return new WorkspaceEntry
        {
            StraumrEntry = entry,
            Display = display,
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
