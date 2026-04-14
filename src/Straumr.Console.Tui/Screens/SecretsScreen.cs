using System.Diagnostics;
using System.IO;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Console;
using Straumr.Console.Tui.Helpers;
using Straumr.Console.Tui.Models;
using Straumr.Console.Tui.Screens.Base;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens;

public sealed class SecretsScreen(
    TuiInteractiveConsole interactiveConsole,
    IStraumrSecretService secretService,
    IStraumrOptionsService optionsService,
    StraumrTheme theme)
    : ModelScreen<SecretEntry>(theme,
        screenTitle: "Secrets",
        emptyStateText: "No secrets defined",
        itemTypeNamePlural: "secrets")
{
    private const string ActionFinish = "Finish";
    private const string ActionSave = "Save";
    private const string ActionName = "Edit name";
    private const string ActionValue = "Edit value";

    protected override string ModelHintsText => $"c Create  d Delete  e Edit  E Edit with " +
                                                $"{Path.GetFileNameWithoutExtension(Environment.GetEnvironmentVariable("EDITOR")) ?? "Undefined"}  y Copy";

    protected override void OnInitialized()
    {
        int secretCount = optionsService.Options.Secrets.Count;
        ShowSuccess($"{secretCount} secret{(secretCount == 1 ? string.Empty : "s")} loaded");
    }

    protected override string GetDisplayText(SecretEntry entry) => entry.Display;

    protected override bool IsSameEntry(SecretEntry? left, SecretEntry? right)
        => left?.Id == right?.Id;

    protected override bool HandleModelKeyDown(Key key, SecretEntry? selectedEntry)
    {
        if (key is { IsCtrl: false, IsAlt: false })
        {
            switch (KeyHelpers.GetCharValue(key))
            {
                case 'c':
                    CreateSecret();
                    return true;
                case 'd':
                    DeleteSecret(selectedEntry);
                    return true;
                case 'e':
                    EditSecret(selectedEntry);
                    return true;
                case 'E':
                    EditSecretWithEditor(selectedEntry);
                    return true;
                case 'y':
                    CopySecret(selectedEntry);
                    return true;
            }
        }

        return false;
    }

    private void CreateSecret()
    {
        SecretEditorState state = SecretEditorState.CreateNew();
        RunSecretEditor(state, null);
    }

    private void EditSecret(SecretEntry? selectedEntry)
    {
        if (selectedEntry is null)
        {
            return;
        }

        if (selectedEntry.IsDamaged)
        {
            EditSecretWithEditor(selectedEntry);
            return;
        }

        StraumrSecret secret;
        try
        {
            secret = secretService.GetAsync(selectedEntry.Identifier).GetAwaiter().GetResult();
        }
        catch (StraumrException ex)
        {
            ShowDanger(ex.Message);
            return;
        }
        catch (Exception ex)
        {
            ShowDanger(ex.Message);
            return;
        }

        SecretEditorState state = SecretEditorState.FromSecret(secret);
        RunSecretEditor(state, secret);
    }

    private void EditSecretWithEditor(SecretEntry? selectedEntry)
    {
        if (selectedEntry is null)
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
            Guid secretId;
            string tempPath;
            try
            {
                (secretId, tempPath) = await secretService.PrepareEditAsync(identifier);
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
                    secretService.ApplyEdit(secretId, tempPath);
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

    private void DeleteSecret(SecretEntry? selectedEntry)
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
            secretService.DeleteAsync(selectedEntry.Identifier).GetAwaiter().GetResult();
            _ = RefreshAsync();
            ShowSuccess($"Deleted secret \"{selectedEntry.Identifier}\"");
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

    private void CopySecret(SecretEntry? selectedEntry)
    {
        if (selectedEntry is null)
        {
            return;
        }

        if (selectedEntry.IsDamaged)
        {
            ShowDanger($"Cannot copy damaged secret \"{selectedEntry.Identifier}\"");
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

        try
        {
            secretService.CopyAsync(selectedEntry.Identifier, newName).GetAwaiter().GetResult();
            _ = RefreshAsync();
            ShowSuccess($"Copied secret to \"{newName}\"");
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

    protected override void OpenSelectedEntry() { }

    protected override void InspectSelectedEntry()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        StraumrSecret? secret = null;
        try
        {
            secret = secretService.PeekByIdAsync(SelectedEntry.Id).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // ignored
        }

        string valueDisplay = secret is not null
            ? MarkupText.ToPlain(secret.Value)
            : "[secondary]N/A[/]";

        interactiveConsole.ShowDetails(
            string.Empty,
            [
                ("ID", $"[bold]{SelectedEntry.Id}[/]"),
                ("Name", SelectedEntry.Name is not null ? $"{SelectedEntry.Identifier}" : "[secondary]N/A[/]"),
                ("Value", valueDisplay),
                ("Modified",
                    SelectedEntry.Modified?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[secondary]N/A[/]"),
                ("Last Accessed",
                    SelectedEntry.LastAccessed?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[secondary]N/A[/]"),
                ("Status", SelectedEntry.IsDamaged ? "[danger][bold]Damaged[/][/]" : "[success][bold]Valid[/][/]")
            ]);
    }

    protected override IEnumerable<ModelCommand> GetCommands()
    {
        yield return new ModelCommand("create", _ => CreateSecret(), "new");
        yield return new ModelCommand("delete", _ => DeleteSecret(SelectedEntry), "rm", "remove");
        yield return new ModelCommand("edit", _ => EditSecret(SelectedEntry));
        yield return new ModelCommand("editor", _ => EditSecretWithEditor(SelectedEntry));
        yield return new ModelCommand("copy", _ => CopySecret(SelectedEntry), "cp");
    }

    protected override async Task<IReadOnlyList<SecretEntry>> LoadEntriesAsync(CancellationToken cancellationToken)
    {
        var entries = new List<SecretEntry>();
        foreach (StraumrSecretEntry entry in optionsService.Options.Secrets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(await BuildSecretEntryAsync(entry));
        }

        return entries
            .OrderBy(item => item.Name is null ? 1 : 0)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .ToList();
    }

    private async Task<SecretEntry> BuildSecretEntryAsync(StraumrSecretEntry entry)
    {
        string identifier = entry.Id.ToString();
        string status = "Valid";
        bool isDamaged = false;
        DateTimeOffset? lastAccessed = null;
        DateTimeOffset? modified = null;
        string? name = null;
        string display;

        try
        {
            StraumrSecret secret = await secretService.PeekByIdAsync(entry.Id);
            identifier = secret.Name;
            name = secret.Name;
            lastAccessed = secret.LastAccessed;
            modified = secret.Modified;

            string line0 = $"[accent]◇[/] [bold]{secret.Name}[/]";
            string line1 = $"  [secondary]{secret.Id}[/]";
            string statsRight =
                $"{FormatSecretValue(secret.Value)}  [/][info]{secret.Modified.LocalDateTime:yyyy-MM-dd}[/]";
            string line2 = $"  [secondary]{statsRight}";
            display = $"{line0}\n{line1}\n{line2}";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            isDamaged = true;
            status = "Corrupt";
            display = $"[danger]X[/] [bold]{entry.Id}[/]  [danger](Corrupt)[/]\n  [danger]Secret file is corrupt[/]";
        }
        catch (StraumrException ex) when (ex.Reason is StraumrError.EntryNotFound or StraumrError.MissingEntry)
        {
            isDamaged = true;
            status = "Missing";
            display = $"[danger]X[/] [bold]{entry.Id}[/]  [warning](Missing)[/]\n  [warning]Secret file is missing[/]";
        }

        return new SecretEntry
        {
            Id = entry.Id,
            Display = display,
            Identifier = identifier,
            Status = status,
            IsDamaged = isDamaged,
            LastAccessed = lastAccessed,
            Modified = modified,
            Name = name,
        };
    }

    private void RunSecretEditor(SecretEditorState state, StraumrSecret? existingSecret)
    {
        string completionAction = existingSecret is null ? ActionFinish : ActionSave;
        string promptTitle = existingSecret is null ? "Create secret" : "Edit secret";

        while (true)
        {
            string? action = interactiveConsole.Select(
                promptTitle,
                [completionAction, ActionName, ActionValue],
                choice => DescribeSecretMenuChoice(choice, state, completionAction));

            if (action is null)
            {
                return;
            }

            if (action == completionAction)
            {
                if (TryPersistSecret(state, existingSecret))
                {
                    return;
                }

                continue;
            }

            HandleSecretEditAction(action, state);
        }
    }

    private string DescribeSecretMenuChoice(string action, SecretEditorState state, string completionAction)
    {
        return action switch
        {
            var value when value == completionAction => completionAction,
            ActionName => $"Name: {FormatSecretName(state.Name)}",
            ActionValue => $"Value: {(string.IsNullOrWhiteSpace(state.Value) ? "[secondary]not set[/]" : "[success]set[/]")}",
            _ => action
        };
    }

    private static string FormatSecretName(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? "[secondary]not set[/]"
            : $"[blue]{name}[/]";
    }

    private bool TryPersistSecret(SecretEditorState state, StraumrSecret? existingSecret)
    {
        if (string.IsNullOrWhiteSpace(state.Name))
        {
            interactiveConsole.ShowMessage("Validation", "A name is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(state.Value))
        {
            interactiveConsole.ShowMessage("Validation", "A value is required.");
            return false;
        }

        try
        {
            if (existingSecret is null)
            {
                StraumrSecret secret = new()
                {
                    Name = state.Name,
                    Value = state.Value
                };
                secretService.CreateAsync(secret).GetAwaiter().GetResult();
                _ = RefreshAsync();
                ShowSuccess($"Created secret \"{secret.Name}\"");
            }
            else
            {
                existingSecret.Name = state.Name;
                existingSecret.Value = state.Value;
                existingSecret.Modified = DateTimeOffset.UtcNow;
                secretService.UpdateAsync(existingSecret).GetAwaiter().GetResult();
                _ = RefreshAsync();
                ShowSuccess($"Updated secret \"{existingSecret.Name}\"");
            }

            return true;
        }
        catch (StraumrException ex)
        {
            ShowDanger(ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            ShowDanger(ex.Message);
            return false;
        }
    }

    private void HandleSecretEditAction(string action, SecretEditorState state)
    {
        switch (action)
        {
            case ActionName:
            {
                string? updated = interactiveConsole.TextInput(
                    "Name",
                    state.Name,
                    validate: value => string.IsNullOrWhiteSpace(value) ? "Name cannot be empty." : null);
                if (!string.IsNullOrWhiteSpace(updated))
                {
                    state.Name = updated;
                }

                break;
            }
            case ActionValue:
            {
                string? value = interactiveConsole.SecretInput("Value");
                if (!string.IsNullOrWhiteSpace(value))
                {
                    state.Value = value;
                }

                break;
            }
        }
    }

    private static string FormatSecretValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "[grey]empty[/]";
        }

        return MarkupText.ToPlain(value);
    }

    private sealed class SecretEditorState
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        public static SecretEditorState CreateNew() => new();

        public static SecretEditorState FromSecret(StraumrSecret secret)
        {
            return new SecretEditorState
            {
                Name = secret.Name,
                Value = secret.Value
            };
        }
    }
}
