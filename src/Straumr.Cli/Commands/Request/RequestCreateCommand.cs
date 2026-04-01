using System.Diagnostics;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Cli.Console;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Console.PromptHelpers;

namespace Straumr.Cli.Commands.Request;

public class RequestCreateCommand(IStraumrRequestService requestService)
    : AsyncCommand<RequestCreateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name>")] public required string Name { get; set; }
        [CommandOption("-e|--editor")] public bool UseEditor { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        if (!settings.UseEditor)
        {
            return await ExecutePromptMenuAsync(settings, cancellation);
        }
        else
        {
            return await ExecuteFileEditAsync(settings, cancellation);
        }
    }

    private async Task<int> ExecutePromptMenuAsync(Settings settings,
        CancellationToken cancellation)
    {
        var console = new EscapeCancellableConsole(AnsiConsole.Console);
        string? url = null;
        var method = "GET";
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bodyType = BodyType.None;
        var bodies = new Dictionary<BodyType, string>();

        const string actionFinish = "Finish";
        const string actionUrl = "Edit URL";
        const string actionMethod = "Edit method";
        const string actionParams = "Edit params";
        const string actionHeaders = "Edit headers";
        const string actionBody = "Edit body";

        while (true)
        {
            string urlDisplay = string.IsNullOrWhiteSpace(url) ? "[grey]not set[/]" : $"[blue]{Markup.Escape(url)}[/]";
            var methodDisplay = $"[blue]{method}[/]";
            string paramsDisplay = parameters.Count == 0 ? "[grey]none[/]" : $"[blue]{parameters.Count}[/]";
            string headersDisplay = headers.Count == 0 ? "[grey]none[/]" : $"[blue]{headers.Count}[/]";
            string bodyDisplay = bodyType == BodyType.None
                ? "[grey]none[/]"
                : $"[blue]{BodyTypeDisplayName(bodyType)}[/]";

            SelectionPrompt<string> prompt = new SelectionPrompt<string>()
                .Title("Request setup")
                .EnableSearch()
                .SearchPlaceholderText("/")
                .UseConverter(choice => choice switch
                {
                    actionUrl => $"URL: {urlDisplay}",
                    actionMethod => $"Method: {methodDisplay}",
                    actionParams => $"Params: {paramsDisplay}",
                    actionHeaders => $"Headers: {headersDisplay}",
                    actionBody => $"Body: {bodyDisplay}",
                    _ => choice
                })
                .AddChoices(actionFinish, actionUrl, actionMethod, actionParams, actionHeaders, actionBody);

            string? action = await PromptAsync(console, prompt);

            if (action is null)
            {
                continue;
            }

            switch (action)
            {
                case actionFinish:
                {
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        ShowTransientMessage("[red]URL is required.[/]");
                        break;
                    }

                    var request = new StraumrRequest
                    {
                        Name = settings.Name,
                        Uri = new Uri(url, UriKind.Absolute),
                        Method = new HttpMethod(method),
                        Params = parameters,
                        Headers = headers,
                        BodyType = bodyType,
                        Bodies = bodies
                    };

                    if (!requestService.Validate(request, out string? validationMessage))
                    {
                        ShowTransientMessage($"[red]{Markup.Escape(validationMessage ?? "Invalid request.")}[/]");
                        break;
                    }

                    try
                    {
                        await requestService.Create(request);
                        AnsiConsole.MarkupLine($"[green]Created request[/] [bold]{request.Name}[/] ({request.Id})");
                        return 0;
                    }
                    catch (StraumrException ex)
                    {
                        ShowTransientMessage($"[red]{Markup.Escape(ex.Message)}[/]");
                    }
                    catch (Exception ex)
                    {
                        ShowTransientMessage($"[red]{Markup.Escape(ex.Message)}[/]");
                    }

                    break;
                }
                case actionUrl:
                {
                    string? updated = await PromptUrlAsync(console);
                    if (!string.IsNullOrWhiteSpace(updated))
                    {
                        url = updated;
                    }

                    break;
                }
                case actionMethod:
                {
                    string? selected = await PromptMethodAsync(console);
                    if (!string.IsNullOrWhiteSpace(selected))
                    {
                        method = selected;
                    }

                    break;
                }
                case actionParams:
                    await EditKeyValuePairsAsync(console, "Params", parameters);
                    break;
                case actionHeaders:
                    await EditKeyValuePairsAsync(console, "Headers", headers);
                    break;
                case actionBody:
                    bodyType = await EditBodyAsync(console, headers, bodies, bodyType, cancellation);
                    break;
            }
        }
    }

    private async Task<int> ExecuteFileEditAsync(Settings settings, CancellationToken cancellation)
    {
        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (editor is null)
        {
            throw new StraumrException("No default editor configured", StraumrError.MissingEntry);
        }

        var console = new EscapeCancellableConsole(AnsiConsole.Console);
        const string actionFinish = "Finish";
        const string actionEdit = "Edit";

        string tempPath = Path.GetTempFileName();
        try
        {
            var request = new StraumrRequest
            {
                Uri = new Uri("https://straumr.app"),
                Method = HttpMethod.Get,
                Name = settings.Name
            };

            string json = JsonSerializer.Serialize(request, StraumrJsonContext.Default.StraumrRequest);
            await File.WriteAllTextAsync(tempPath, json, cancellation);

            var needsEditorLaunch = true;

            while (true)
            {
                if (needsEditorLaunch)
                {
                    int? exitCode = await LaunchEditorAsync(editor, tempPath, cancellation);
                    if (exitCode is not null)
                    {
                        return exitCode.Value;
                    }

                    needsEditorLaunch = false;
                    continue;
                }

                string? action =
                    await PromptMenuAsync(console, $"Creating request {settings.Name}", [actionFinish, actionEdit]);

                if (action is null)
                {
                    continue;
                }

                if (action == actionEdit)
                {
                    needsEditorLaunch = true;
                    continue;
                }

                if (action == actionFinish)
                {
                    string editedJson = await File.ReadAllTextAsync(tempPath, cancellation);
                    StraumrRequest? deserializedJson;
                    try
                    {
                        deserializedJson = JsonSerializer.Deserialize<StraumrRequest>(editedJson,
                            StraumrJsonContext.Default.StraumrRequest);
                    }
                    catch (JsonException)
                    {
                        ShowTransientMessage("[red]Invalid request JSON. Please fix the file in the editor.[/]");
                        continue;
                    }

                    if (deserializedJson is null)
                    {
                        ShowTransientMessage("[red]Invalid request. Please fix the file in the editor.[/]");
                        continue;
                    }

                    if (!requestService.Validate(deserializedJson, out string? validationMessage))
                    {
                        ShowTransientMessage($"[red]{Markup.Escape(validationMessage ?? "Invalid request.")}[/]");
                        continue;
                    }

                    try
                    {
                        await requestService.Create(deserializedJson);
                        AnsiConsole.MarkupLine(
                            $"[green]Created request[/] [bold]{deserializedJson.Name}[/] ({deserializedJson.Id})");
                        return 0;
                    }
                    catch (StraumrException ex)
                    {
                        ShowTransientMessage($"[red]{Markup.Escape(ex.Message)}[/]");
                    }
                    catch (Exception ex)
                    {
                        ShowTransientMessage($"[red]{Markup.Escape(ex.Message)}[/]");
                    }
                }
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static async Task<string?> PromptUrlAsync(EscapeCancellableConsole console)
    {
        return await PromptAsync(console,
            new TextPrompt<string>("URL")
                .Validate(value => Uri.TryCreate(value, UriKind.Absolute, out _)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Please enter a valid absolute URL.")));
    }

    private static async Task<string?> PromptMethodAsync(EscapeCancellableConsole console)
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

    private static async Task EditKeyValuePairsAsync(
        EscapeCancellableConsole console, string title, IDictionary<string, string> items)
    {
        string titleLower = title.ToLowerInvariant();

        while (true)
        {
            string? action = await PromptMenuAsync(
                console,
                title,
                ["Back", "Add or update", "Remove", "List"]);

            if (action is null || action == "Back")
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

                    string? value = await PromptAsync(console, new TextPrompt<string>("Value"));
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

    private static async Task<BodyType> EditBodyAsync(
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
            const string actionContent = "Edit content";
            const string actionClear = "Clear content";

            var choices = currentType == BodyType.None
                ? new[] { actionBack, actionType }
                : new[] { actionBack, actionType, actionContent, actionClear };

            var prompt = new SelectionPrompt<string>()
                .Title("Body")
                .EnableSearch()
                .SearchPlaceholderText("/")
                .UseConverter(choice => choice switch
                {
                    actionType => $"Type: {typeDisplay}",
                    actionContent => $"Content: {contentDisplay}",
                    _ => choice
                })
                .AddChoices(choices);

            string? action = await PromptAsync(console, prompt);

            if (action is null || action == actionBack)
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
                        string defaultContent = current ?? DefaultBodyContent(currentType);
                        string? edited = await EditBodyWithEditor(defaultContent, cancellation);
                        if (!string.IsNullOrWhiteSpace(edited))
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
                    bodies.Remove(currentType);
                    break;
            }
        }
    }

    private static string DefaultBodyContent(BodyType type) => type switch
    {
        BodyType.Json => "{\n  \n}",
        BodyType.Xml => "<root>\n  \n</root>",
        _ => ""
    };

    private static async Task<string?> EditFormBodyAsync(
        EscapeCancellableConsole console, string? currentBody)
    {
        var fields = ParseFormFields(currentBody);

        while (true)
        {
            string fieldsDisplay = fields.Count == 0 ? "[grey]none[/]" : $"[blue]{fields.Count}[/]";

            const string actionBack = "Back";
            const string actionAdd = "Add or update";
            const string actionRemove = "Remove";
            const string actionList = "List";

            var prompt = new SelectionPrompt<string>()
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

            if (action is null || action == actionBack)
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

                    string? value = await PromptAsync(console, new TextPrompt<string>("Field value"));
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
        var fields = ParseFormFields(currentBody);

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

            var prompt = new SelectionPrompt<string>()
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

            if (action is null || action == actionBack)
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

                    string? value = await PromptAsync(console, new TextPrompt<string>("Field value"));
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

                    fields[key] = $"@{path}";
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
                        fields.OrderBy(k => k.Key).Select(kv =>
                            kv.Value.StartsWith('@')
                                ? (kv.Key, $"[file] {kv.Value[1..]}")
                                : (kv.Key, kv.Value)),
                        "[yellow]No fields set.[/]");
                    break;
                }
            }
        }
    }

    private static Dictionary<string, string> ParseFormFields(string? body)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
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

    private static async Task<int?> LaunchEditorAsync(string editor, string path, CancellationToken cancellation)
    {
        Process? process = Process.Start(new ProcessStartInfo(editor, path)
        {
            UseShellExecute = false
        });

        if (process is null)
        {
            AnsiConsole.MarkupLine("[red]Editor exited with an error.[/]");
            return 1;
        }

        await process.WaitForExitAsync(cancellation);

        if (process.ExitCode != 0)
        {
            ShowTransientMessage("[red]Editor exited with an error. Changes discarded.[/]");
            return process.ExitCode;
        }

        return null;
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

    private static string BodyTypeDisplayName(BodyType type) => type switch
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
