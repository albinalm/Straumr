using System.Diagnostics;
using System.Text.RegularExpressions;
using Spectre.Console;
using Straumr.Cli.Console;
using Straumr.Core.Enums;
using static Straumr.Cli.Console.PromptHelpers;

namespace Straumr.Cli.Helpers;

internal static class HttpCommandHelpers
{
    private static readonly Regex SecretPattern = new(@"\{\{secret:[^}]+\}\}", RegexOptions.Compiled);

    internal static async Task<string?> PromptUrlAsync(EscapeCancellableConsole console, string? current = null)
    {
        TextPrompt<string> prompt = new TextPrompt<string>("URL")
            .Validate(value => IsValidAbsoluteUrl(value)
                ? ValidationResult.Success()
                : ValidationResult.Error("Please enter a valid absolute URL."));

        return await PromptTextAsync(console, prompt, current);
    }

    internal static bool IsValidAbsoluteUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = SecretPattern.Replace(value, "secret");
        return Uri.TryCreate(normalized, UriKind.Absolute, out _);
    }

    internal static async Task<string?> PromptMethodAsync(EscapeCancellableConsole console)
    {
        return await PromptAsync(console,
            new SelectionPrompt<string>()
                .Title("Method")
                .EnableSearch()
                .SearchPlaceholderText("/")
                .AddChoices(
                    "GET",
                    "POST",
                    "PUT",
                    "PATCH",
                    "DELETE",
                    "HEAD",
                    "OPTIONS",
                    "TRACE",
                    "CONNECT"));
    }

    internal static async Task EditKeyValuePairsAsync(
        EscapeCancellableConsole console, string title, IDictionary<string, string> items)
    {
        string titleLower = title.ToLowerInvariant();

        while (true)
        {
            string? action = await PromptMenuAsync(
                console,
                title,
                ["Back", "Add or update", "Remove", "List"]);

            if (action is null or "Back")
            {
                return;
            }

            switch (action)
            {
                case "Add or update":
                {
                    string? key = await PromptAsync(console,
                        new TextPrompt<string>("Name")
                            .Validate(value => string.IsNullOrWhiteSpace(value)
                                ? ValidationResult.Error("Name cannot be empty.")
                                : ValidationResult.Success()));

                    if (key is null)
                    {
                        break;
                    }

                    string? existing = items.TryGetValue(key, out string? currentValue) ? currentValue : null;
                    string? value = await PromptTextAsync(console, new TextPrompt<string>("Value"), existing);
                    if (value is null)
                    {
                        break;
                    }

                    items[key] = value;
                    break;
                }
                case "Remove":
                {
                    if (items.Count == 0)
                    {
                        ShowTransientMessage($"[yellow]No {titleLower} to remove.[/]");
                        break;
                    }

                    string? key = await PromptMenuAsync(
                        console,
                        "Select to remove",
                        items.Keys.OrderBy(k => k));

                    if (key is not null)
                    {
                        items.Remove(key);
                    }

                    break;
                }
                case "List":
                {
                    ShowTransientTable("Name", "Value",
                        items.OrderBy(k => k.Key).Select(kv => (kv.Key, kv.Value)),
                        $"[yellow]No {titleLower} set.[/]");
                    break;
                }
            }
        }
    }

    internal static async Task<BodyType> EditBodyAsync(
        EscapeCancellableConsole console, IDictionary<string, string> headers,
        Dictionary<BodyType, string> bodies, BodyType currentType, CancellationToken cancellation)
    {
        while (true)
        {
            string typeDisplay = currentType == BodyType.None
                ? "[grey]none[/]"
                : $"[blue]{BodyTypeDisplayName(currentType)}[/]";

            bool hasContent = currentType != BodyType.None && bodies.ContainsKey(currentType);
            string contentDisplay = hasContent ? "[blue]set[/]" : "[grey]empty[/]";

            const string actionBack = "Back";
            const string actionType = "Body type";
            const string actionContent = "Edit body";
            const string actionClear = "Clear body";

            SelectionPrompt<string> prompt = new SelectionPrompt<string>()
                .Title("Body")
                .EnableSearch()
                .SearchPlaceholderText("/")
                .UseConverter(choice => choice switch
                {
                    actionType => $"Type: {typeDisplay}",
                    actionContent => $"Content: {contentDisplay}",
                    _ => choice
                })
                .AddChoices(actionBack, actionType, actionContent, actionClear);

            string? action = await PromptAsync(console, prompt);
            if (action is null or actionBack)
            {
                return currentType;
            }

            switch (action)
            {
                case actionType:
                {
                    string? selected = await PromptMenuAsync(console, "Select body type",
                    [
                        "No body", "JSON", "XML", "Text",
                        "Form URL Encoded", "Multipart Form", "Raw"
                    ]);

                    if (selected is not null)
                    {
                        currentType = selected switch
                        {
                            "No body" => BodyType.None,
                            "JSON" => BodyType.Json,
                            "XML" => BodyType.Xml,
                            "Text" => BodyType.Text,
                            "Form URL Encoded" => BodyType.FormUrlEncoded,
                            "Multipart Form" => BodyType.MultipartForm,
                            "Raw" => BodyType.Raw,
                            _ => currentType
                        };

                        SyncContentTypeHeader(headers, currentType);
                    }

                    break;
                }
                case actionContent:
                {
                    string? current = bodies.GetValueOrDefault(currentType);

                    if (currentType == BodyType.FormUrlEncoded)
                    {
                        string? edited = await EditFormBodyAsync(console, current);
                        if (edited is not null)
                        {
                            bodies[currentType] = edited;
                        }
                        else
                        {
                            bodies.Remove(currentType);
                        }
                    }
                    else if (currentType == BodyType.MultipartForm)
                    {
                        string? edited = await EditMultipartBodyAsync(console, current);
                        if (edited is not null)
                        {
                            bodies[currentType] = edited;
                        }
                        else
                        {
                            bodies.Remove(currentType);
                        }
                    }
                    else
                    {
                        string defaultContent = current ?? string.Empty;
                        string? edited = await EditBodyWithEditor(defaultContent, cancellation);

                        if (edited is not null)
                        {
                            bodies[currentType] = edited;
                        }
                        else
                        {
                            bodies.Remove(currentType);
                        }
                    }

                    break;
                }
                case actionClear:
                {
                    bodies.Remove(currentType);
                    break;
                }
            }
        }
    }

    private static async Task<string?> EditFormBodyAsync(
        EscapeCancellableConsole console, string? currentBody)
    {
        Dictionary<string, string> fields = ParseFormFields(currentBody);

        while (true)
        {
            string fieldsDisplay = fields.Count == 0
                ? "[grey]none[/]"
                : $"[blue]{fields.Count}[/]";

            const string actionBack = "Back";
            const string actionAdd = "Add or update";
            const string actionRemove = "Remove";
            const string actionList = "List";

            SelectionPrompt<string> prompt = new SelectionPrompt<string>()
                .Title("Form fields")
                .EnableSearch()
                .SearchPlaceholderText("/")
                .UseConverter(choice => choice switch
                {
                    actionAdd => $"Add or update ({fieldsDisplay})",
                    _ => choice
                })
                .AddChoices(actionBack, actionAdd, actionRemove, actionList);

            string? action = await PromptAsync(console, prompt);

            if (action is null or actionBack)
            {
                return SerializeFormFields(fields);
            }

            switch (action)
            {
                case actionAdd:
                {
                    string? key = await PromptAsync(console,
                        new TextPrompt<string>("Field name")
                            .Validate(value => string.IsNullOrWhiteSpace(value)
                                ? ValidationResult.Error("Field name cannot be empty.")
                                : ValidationResult.Success()));

                    if (key is null)
                    {
                        break;
                    }

                    string? existing = fields.GetValueOrDefault(key);
                    string? value = await PromptTextAsync(console, new TextPrompt<string>("Field value"), existing);
                    if (value is null)
                    {
                        break;
                    }

                    fields[key] = value;
                    break;
                }
                case actionRemove:
                {
                    if (fields.Count == 0)
                    {
                        ShowTransientMessage("[yellow]No fields to remove.[/]");
                        break;
                    }

                    string? key = await PromptMenuAsync(console, "Select field to remove",
                        fields.Keys.OrderBy(k => k));

                    if (key is not null)
                    {
                        fields.Remove(key);
                    }

                    break;
                }
                case actionList:
                {
                    ShowTransientTable("Name", "Value",
                        fields.OrderBy(k => k.Key).Select(kv => (kv.Key, kv.Value)),
                        "[yellow]No fields set.[/]");
                    break;
                }
            }
        }
    }

    private static async Task<string?> EditMultipartBodyAsync(
        EscapeCancellableConsole console, string? currentBody)
    {
        Dictionary<string, string> fields = ParseFormFields(currentBody);

        while (true)
        {
            int textCount = fields.Count(kv => !kv.Value.StartsWith('@'));
            int fileCount = fields.Count(kv => kv.Value.StartsWith('@'));
            string fieldsDisplay = fields.Count == 0
                ? "[grey]none[/]"
                : $"[blue]{textCount} text, {fileCount} file[/]";

            const string actionBack = "Back";
            const string actionAddText = "Add text field";
            const string actionAddFile = "Add file field";
            const string actionRemove = "Remove";
            const string actionList = "List";

            SelectionPrompt<string> prompt = new SelectionPrompt<string>()
                .Title("Multipart form fields")
                .EnableSearch()
                .SearchPlaceholderText("/")
                .UseConverter(choice => choice switch
                {
                    actionAddText => $"Add text field ({fieldsDisplay})",
                    _ => choice
                })
                .AddChoices(actionBack, actionAddText, actionAddFile, actionRemove, actionList);

            string? action = await PromptAsync(console, prompt);

            if (action is null or actionBack)
            {
                return SerializeFormFields(fields);
            }

            switch (action)
            {
                case actionAddText:
                {
                    string? key = await PromptAsync(console,
                        new TextPrompt<string>("Field name")
                            .Validate(value => string.IsNullOrWhiteSpace(value)
                                ? ValidationResult.Error("Field name cannot be empty.")
                                : ValidationResult.Success()));

                    if (key is null)
                    {
                        break;
                    }

                    string? existing = fields.GetValueOrDefault(key);
                    string? value = await PromptTextAsync(console, new TextPrompt<string>("Field value"), existing);
                    if (value is null)
                    {
                        break;
                    }

                    fields[key] = value;
                    break;
                }
                case actionAddFile:
                {
                    string? key = await PromptAsync(console,
                        new TextPrompt<string>("Field name")
                            .Validate(value => string.IsNullOrWhiteSpace(value)
                                ? ValidationResult.Error("Field name cannot be empty.")
                                : ValidationResult.Success()));

                    if (key is null)
                    {
                        break;
                    }

                    string? path = await PromptAsync(console,
                        new TextPrompt<string>("File path")
                            .Validate(value => string.IsNullOrWhiteSpace(value)
                                ? ValidationResult.Error("File path cannot be empty.")
                                : !File.Exists(value)
                                    ? ValidationResult.Error("File not found.")
                                    : ValidationResult.Success()));

                    if (path is null)
                    {
                        break;
                    }

                    fields[key] = '@' + path;
                    break;
                }
                case actionRemove:
                {
                    if (fields.Count == 0)
                    {
                        ShowTransientMessage("[yellow]No fields to remove.[/]");
                        break;
                    }

                    string? key = await PromptMenuAsync(console, "Select field to remove",
                        fields.Keys.OrderBy(k => k));

                    if (key is not null)
                    {
                        fields.Remove(key);
                    }

                    break;
                }
                case actionList:
                {
                    IEnumerable<(string Key, string Value)> rows = fields
                        .OrderBy(k => k.Key)
                        .Select(kv =>
                        {
                            string value = kv.Value.StartsWith('@')
                                ? $"@{Path.GetFileName(kv.Value[1..])}"
                                : kv.Value;
                            return (kv.Key, value);
                        });

                    ShowTransientTable("Name", "Value", rows, "[yellow]No fields set.[/]");
                    break;
                }
            }
        }
    }

    private static async Task<string?> EditBodyWithEditor(string content, CancellationToken cancellation)
    {
        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (string.IsNullOrWhiteSpace(editor))
        {
            ShowTransientMessage("[red]No default editor is configured. Set $EDITOR to use this feature.[/]");
            return content;
        }

        string tempPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempPath, content, cancellation);

            Process? process = Process.Start(new ProcessStartInfo(editor, tempPath)
            {
                UseShellExecute = false
            });

            if (process is null)
            {
                ShowTransientMessage("[red]Failed to open file in default editor[/]");
                return content;
            }

            await process.WaitForExitAsync(cancellation);

            if (process.ExitCode != 0)
            {
                ShowTransientMessage("[red]Editor exited with an error. Changes discarded.[/]");
                return content;
            }

            string edited = await File.ReadAllTextAsync(tempPath, cancellation);
            return string.IsNullOrWhiteSpace(edited) ? null : edited;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static Dictionary<string, string> ParseFormFields(string? body)
    {
        var fields = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(body))
        {
            return fields;
        }

        foreach (string pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            if (eq < 0)
            {
                fields[Uri.UnescapeDataString(pair)] = string.Empty;
            }
            else
            {
                fields[Uri.UnescapeDataString(pair[..eq])] = Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
        }

        return fields;
    }

    private static string? SerializeFormFields(Dictionary<string, string> fields)
    {
        if (fields.Count == 0)
        {
            return null;
        }

        return string.Join('&', fields.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    private static void SyncContentTypeHeader(IDictionary<string, string> headers, BodyType type)
    {
        string? contentType = type switch
        {
            BodyType.Json => "application/json",
            BodyType.Xml => "application/xml",
            BodyType.Text => "text/plain",
            BodyType.FormUrlEncoded => "application/x-www-form-urlencoded",
            BodyType.MultipartForm => "multipart/form-data",
            _ => null
        };

        if (contentType is not null)
        {
            headers["Content-Type"] = contentType;
        }
        else
        {
            headers.Remove("Content-Type");
        }
    }

    internal static string BodyTypeDisplayName(BodyType type)
    {
        return type switch
        {
            BodyType.None => "No body",
            BodyType.Json => "JSON (application/json)",
            BodyType.Xml => "XML (application/xml)",
            BodyType.Text => "Text (text/plain)",
            BodyType.FormUrlEncoded => "Form URL Encoded (application/x-www-form-urlencoded)",
            BodyType.MultipartForm => "Multipart Form (multipart/form-data)",
            BodyType.Raw => "Raw (no Content-Type header)",
            _ => type.ToString()
        };
    }
}
