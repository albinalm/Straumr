using System.Diagnostics;
using Straumr.Console.Shared.Helpers;
using Straumr.Console.Tui.Console;
using Straumr.Console.Tui.Services.Interfaces;
using Straumr.Core.Enums;

namespace Straumr.Console.Tui.Services;

public sealed class BodyEditor(TuiInteractiveConsole interactiveConsole) : IBodyEditor
{
    private const string ActionBack = "Back";
    private const string ActionType = "Body type";
    private const string ActionEdit = "Edit body";
    private const string ActionClear = "Clear body";

    public BodyType Edit(
        IDictionary<string, string> headers,
        Dictionary<BodyType, string> bodies,
        BodyType currentType)
    {
        while (true)
        {
            string typeDisplay = currentType == BodyType.None
                ? "[secondary]none[/]"
                : $"[blue]{RequestEditingHelpers.BodyTypeDisplayName(currentType)}[/]";

            bool hasContent = currentType != BodyType.None && bodies.ContainsKey(currentType);
            string contentDisplay = hasContent ? "[success]set[/]" : "[secondary]empty[/]";

            string? action = interactiveConsole.Select(
                "Body",
                [ActionBack, ActionType, ActionEdit, ActionClear],
                choice => choice switch
                {
                    ActionType => $"Type: {typeDisplay}",
                    ActionEdit => $"Content: {contentDisplay}",
                    _ => choice
                });

            if (action is null or ActionBack)
            {
                return currentType;
            }

            switch (action)
            {
                case ActionType:
                    currentType = PromptBodyType(currentType, headers);
                    break;
                case ActionEdit:
                    EditContent(bodies, currentType);
                    break;
                case ActionClear:
                    bodies.Remove(currentType);
                    break;
            }
        }
    }

    private BodyType PromptBodyType(BodyType currentType, IDictionary<string, string> headers)
    {
        string? selected = interactiveConsole.Select(
            "Select body type",
            ["No body", "JSON", "XML", "Text", "Form URL Encoded", "Multipart Form", "Raw"]);

        if (selected is null)
        {
            return currentType;
        }

        BodyType nextType = selected switch
        {
            "JSON" => BodyType.Json,
            "XML" => BodyType.Xml,
            "Text" => BodyType.Text,
            "Form URL Encoded" => BodyType.FormUrlEncoded,
            "Multipart Form" => BodyType.MultipartForm,
            "Raw" => BodyType.Raw,
            _ => BodyType.None
        };

        SyncContentTypeHeader(headers, nextType);
        return nextType;
    }

    private void EditContent(Dictionary<BodyType, string> bodies, BodyType currentType)
    {
        if (currentType == BodyType.None)
        {
            return;
        }

        bodies.TryGetValue(currentType, out string? current);

        string? edited = currentType switch
        {
            BodyType.FormUrlEncoded => EditFormBody(current),
            BodyType.MultipartForm => EditMultipartBody(current),
            _ => EditWithExternalEditor(current ?? string.Empty)
        };

        if (edited is null)
        {
            bodies.Remove(currentType);
        }
        else
        {
            bodies[currentType] = edited;
        }
    }

    private string? EditFormBody(string? currentBody)
    {
        Dictionary<string, string> fields = ParseFormFields(currentBody);

        while (true)
        {
            string? action = interactiveConsole.Select(
                "Form fields",
                [ActionBack, "Add or update", "Remove", "List"]);

            if (action is null or ActionBack)
            {
                return SerializeFormFields(fields);
            }

            switch (action)
            {
                case "Add or update":
                    AddOrUpdateField(fields, "Field value");
                    break;
                case "Remove":
                    RemoveField(fields);
                    break;
                case "List":
                    ShowFields(fields, formatFileValue: false);
                    break;
            }
        }
    }

    private string? EditMultipartBody(string? currentBody)
    {
        Dictionary<string, string> fields = ParseFormFields(currentBody);

        while (true)
        {
            string? action = interactiveConsole.Select(
                "Multipart form fields",
                [ActionBack, "Add text field", "Add file field", "Remove", "List"]);

            if (action is null or ActionBack)
            {
                return SerializeFormFields(fields);
            }

            switch (action)
            {
                case "Add text field":
                    AddOrUpdateField(fields, "Field value");
                    break;
                case "Add file field":
                    AddFileField(fields);
                    break;
                case "Remove":
                    RemoveField(fields);
                    break;
                case "List":
                    ShowFields(fields, formatFileValue: true);
                    break;
            }
        }
    }

    private void AddOrUpdateField(Dictionary<string, string> fields, string valueLabel)
    {
        string? key = interactiveConsole.TextInput(
            "Field name",
            validate: value => string.IsNullOrWhiteSpace(value) ? "Field name cannot be empty." : null);
        if (key is null)
        {
            return;
        }

        fields.TryGetValue(key, out string? existing);
        string? value = interactiveConsole.TextInput(valueLabel, existing);
        if (value is not null)
        {
            fields[key] = value;
        }
    }

    private void AddFileField(Dictionary<string, string> fields)
    {
        string? key = interactiveConsole.TextInput(
            "Field name",
            validate: value => string.IsNullOrWhiteSpace(value) ? "Field name cannot be empty." : null);
        if (key is null)
        {
            return;
        }

        string? path = interactiveConsole.TextInput("File path");
        if (!string.IsNullOrWhiteSpace(path))
        {
            fields[key] = path.StartsWith('@') ? path : $"@{path}";
        }
    }

    private void RemoveField(Dictionary<string, string> fields)
    {
        if (fields.Count == 0)
        {
            interactiveConsole.ShowMessage("No fields to remove.");
            return;
        }

        string? key = interactiveConsole.Select(
            "Select field to remove",
            fields.Keys.OrderBy(k => k).ToList());
        if (key is not null)
        {
            fields.Remove(key);
        }
    }

    private void ShowFields(Dictionary<string, string> fields, bool formatFileValue)
    {
        IEnumerable<(string Key, string Value)> rows = fields
            .OrderBy(k => k.Key)
            .Select(kv =>
            {
                string value = formatFileValue && kv.Value.StartsWith('@')
                    ? $"@{Path.GetFileName(kv.Value[1..])}"
                    : kv.Value;
                return (kv.Key, value);
            });

        interactiveConsole.ShowTable("Name", "Value", rows, "No fields set.");
    }

    private string? EditWithExternalEditor(string content)
    {
        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (string.IsNullOrWhiteSpace(editor))
        {
            interactiveConsole.ShowMessage("Editor", "Set the $EDITOR environment variable to edit body content.");
            return content;
        }

        string tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath, content);

            Process? process = Process.Start(new ProcessStartInfo(editor, tempPath)
            {
                UseShellExecute = false
            });

            if (process is null)
            {
                interactiveConsole.ShowMessage("Editor", "Failed to launch editor.");
                return content;
            }

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                interactiveConsole.ShowMessage("Editor", "Editor exited with an error. Changes discarded.");
                return content;
            }

            string edited = File.ReadAllText(tempPath);
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
        Dictionary<string, string> fields = new();
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

    private static void SyncContentTypeHeader(IDictionary<string, string> headers, BodyType bodyType)
    {
        const string contentTypeHeader = "Content-Type";
        switch (bodyType)
        {
            case BodyType.Json:
                headers[contentTypeHeader] = "application/json";
                break;
            case BodyType.Xml:
                headers[contentTypeHeader] = "application/xml";
                break;
            case BodyType.Text:
                headers[contentTypeHeader] = "text/plain";
                break;
            case BodyType.FormUrlEncoded:
                headers[contentTypeHeader] = "application/x-www-form-urlencoded";
                break;
            case BodyType.MultipartForm:
                headers[contentTypeHeader] = "multipart/form-data";
                break;
            default:
                headers.Remove(contentTypeHeader);
                break;
        }
    }
}
