using System.Diagnostics;
using Straumr.Console.Shared.Interfaces;
using Straumr.Console.Shared.Helpers;
using Straumr.Core.Enums;
using Straumr.Core.Helpers;

namespace Straumr.Console.Cli.Helpers;

internal static class HttpCommandHelpers
{
    internal static string? PromptUrl(IInteractiveConsole console, string? current = null)
    {
        return console.TextInput("URL", current,
            validate: value => IsValidAbsoluteUrl(value) ? null : "Please enter a valid absolute URL.");
    }

    internal static bool IsValidAbsoluteUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = SecretHelpers.SecretPattern.Replace(value, "secret");
        return Uri.TryCreate(normalized, UriKind.Absolute, out _);
    }

    internal static string? PromptMethod(IInteractiveConsole console)
    {
        return console.Select("Method",
            ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "TRACE", "CONNECT"]);
    }

    internal static void EditKeyValuePairs(
        IInteractiveConsole console, string title, IDictionary<string, string> items, Action? onSaved = null)
    {
        if (console.TryEditKeyValuePairs(title, items, onSaved))
        {
            return;
        }

        string titleLower = title.ToLowerInvariant();

        while (true)
        {
            string? action = console.Select(title, ["Back", "Add or update", "Remove", "List"]);

            if (action is null or "Back")
            {
                return;
            }

            switch (action)
            {
                case "Add or update":
                {
                    string? key = console.TextInput("Name",
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Name cannot be empty." : null);

                    if (key is null)
                    {
                        break;
                    }

                    string? existing = items.TryGetValue(key, out string? currentValue) ? currentValue : null;
                    string? value = console.TextInput("Value", existing);
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
                        console.ShowMessage($"[yellow]No {titleLower} to remove.[/]");
                        break;
                    }

                    string? key = console.Select("Select to remove",
                        items.Keys.OrderBy(k => k).ToList());

                    if (key is not null)
                    {
                        items.Remove(key);
                    }

                    break;
                }
                case "List":
                {
                    console.ShowTable("Name", "Value",
                        items.OrderBy(k => k.Key).Select(kv => (kv.Key, kv.Value)),
                        $"[yellow]No {titleLower} set.[/]");
                    break;
                }
            }
        }
    }

    internal static async Task<BodyType> EditBodyAsync(
        IInteractiveConsole console, IDictionary<string, string> headers,
        Dictionary<BodyType, string> bodies, BodyType currentType, CancellationToken cancellation)
    {
        while (true)
        {
            string typeDisplay = currentType == BodyType.None
                ? "[grey]none[/]"
                : $"[blue]{RequestEditingHelpers.BodyTypeDisplayName(currentType)}[/]";

            bool hasContent = currentType != BodyType.None && bodies.ContainsKey(currentType);
            string contentDisplay = hasContent ? "[blue]set[/]" : "[grey]empty[/]";

            const string actionBack = "Back";
            const string actionType = "Body type";
            const string actionContent = "Edit body";
            const string actionClear = "Clear body";

            string? action = console.Select("Body",
                [actionBack, actionType, actionContent, actionClear],
                choice => choice switch
                {
                    actionType => $"Type: {typeDisplay}",
                    actionContent => $"Content: {contentDisplay}",
                    _ => choice
                });

            if (action is null or actionBack)
            {
                return currentType;
            }

            switch (action)
            {
                case actionType:
                {
                    string? selected = console.Select("Select body type",
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
                        string? edited = EditFormBody(console, current);
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
                        string? edited = EditMultipartBody(console, current);
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
                        string? edited = await EditBodyWithEditor(console, defaultContent, cancellation);

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

    private static string? EditFormBody(
        IInteractiveConsole console, string? currentBody)
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

            string? action = console.Select("Form fields",
                [actionBack, actionAdd, actionRemove, actionList],
                choice => choice switch
                {
                    actionAdd => $"Add or update ({fieldsDisplay})",
                    _ => choice
                });

            if (action is null or actionBack)
            {
                return SerializeFormFields(fields);
            }

            switch (action)
            {
                case actionAdd:
                {
                    string? key = console.TextInput("Field name",
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Field name cannot be empty." : null);

                    if (key is null)
                    {
                        break;
                    }

                    string? existing = fields.GetValueOrDefault(key);
                    string? value = console.TextInput("Field value", existing);
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
                        console.ShowMessage("[yellow]No fields to remove.[/]");
                        break;
                    }

                    string? key = console.Select("Select field to remove",
                        fields.Keys.OrderBy(k => k).ToList());

                    if (key is not null)
                    {
                        fields.Remove(key);
                    }

                    break;
                }
                case actionList:
                {
                    console.ShowTable("Name", "Value",
                        fields.OrderBy(k => k.Key).Select(kv => (kv.Key, kv.Value)),
                        "[yellow]No fields set.[/]");
                    break;
                }
            }
        }
    }

    private static string? EditMultipartBody(
        IInteractiveConsole console, string? currentBody)
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

            string? action = console.Select("Multipart form fields",
                [actionBack, actionAddText, actionAddFile, actionRemove, actionList],
                choice => choice switch
                {
                    actionAddText => $"Add text field ({fieldsDisplay})",
                    _ => choice
                });

            if (action is null or actionBack)
            {
                return SerializeFormFields(fields);
            }

            switch (action)
            {
                case actionAddText:
                {
                    string? key = console.TextInput("Field name",
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Field name cannot be empty." : null);

                    if (key is null)
                    {
                        break;
                    }

                    string? existing = fields.GetValueOrDefault(key);
                    string? value = console.TextInput("Field value", existing);
                    if (value is null)
                    {
                        break;
                    }

                    fields[key] = value;
                    break;
                }
                case actionAddFile:
                {
                    string? key = console.TextInput("Field name",
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Field name cannot be empty." : null);

                    if (key is null)
                    {
                        break;
                    }

                    string? path = console.TextInput("File path",
                        validate: value => string.IsNullOrWhiteSpace(value)
                            ? "File path cannot be empty."
                            : !File.Exists(value)
                                ? "File not found."
                                : null);

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
                        console.ShowMessage("[yellow]No fields to remove.[/]");
                        break;
                    }

                    string? key = console.Select("Select field to remove",
                        fields.Keys.OrderBy(k => k).ToList());

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

                    console.ShowTable("Name", "Value", rows, "[yellow]No fields set.[/]");
                    break;
                }
            }
        }
    }

    private static async Task<string?> EditBodyWithEditor(
        IInteractiveConsole console, string content, CancellationToken cancellation)
    {
        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (string.IsNullOrWhiteSpace(editor))
        {
            console.ShowMessage("[red]No default editor is configured. Set $EDITOR to use this feature.[/]");
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
                console.ShowMessage("[red]Failed to open file in default editor[/]");
                return content;
            }

            await process.WaitForExitAsync(cancellation);

            if (process.ExitCode != 0)
            {
                console.ShowMessage("[red]Editor exited with an error. Changes discarded.[/]");
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
        Dictionary<string, string> fields = new Dictionary<string, string>();
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
            $"{RequestEditingHelpers.EscapeFormFieldComponent(kv.Key)}={RequestEditingHelpers.EscapeFormFieldComponent(kv.Value)}"));
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

    internal static string BodyTypeDisplayName(BodyType type) => RequestEditingHelpers.BodyTypeDisplayName(type);
}
