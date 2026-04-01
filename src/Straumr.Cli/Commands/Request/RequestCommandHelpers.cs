using System.Diagnostics;
using Spectre.Console;
using Straumr.Cli.Console;
using Straumr.Core.Enums;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Console.PromptHelpers;

namespace Straumr.Cli.Commands.Request;

internal static class RequestCommandHelpers
{
    internal static async Task<string?> PromptUrlAsync(EscapeCancellableConsole console)
    {
        return await PromptAsync(console,
            new TextPrompt<string>("URL")
                .Validate(value => Uri.TryCreate(value, UriKind.Absolute, out _)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Please enter a valid absolute URL.")));
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
            const string actionContent = "Edit content";
            const string actionClear = "Clear content";

            string[] choices = currentType == BodyType.None
                ? [actionBack, actionType]
                : [actionBack, actionType, actionContent, actionClear];

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

    internal static async Task<int?> LaunchEditorAsync(string editor, string path, CancellationToken cancellation)
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

    internal static string AuthDisplayName(StraumrAuthConfig? auth)
    {
        return auth switch
        {
            null => "[grey]none[/]",
            BearerAuthConfig bearer => !string.IsNullOrWhiteSpace(bearer.Token)
                ? "[blue]Bearer[/] [green]set[/]"
                : "[blue]Bearer[/] [grey]not set[/]",
            BasicAuthConfig basic => !string.IsNullOrWhiteSpace(basic.Username)
                ? $"[blue]Basic[/] [green]{Markup.Escape(basic.Username)}[/]"
                : "[blue]Basic[/] [grey]not set[/]",
            OAuth2Config oauth2 => FormatOAuth2Display(oauth2),
            CustomAuthConfig custom => FormatCustomAuthDisplay(custom),
            _ => "[grey]none[/]"
        };
    }

    private static string FormatCustomAuthDisplay(CustomAuthConfig config)
    {
        string valueStatus = config.CachedValue is not null ? "[green]has value[/]" : "[grey]no value[/]";
        string sourceDisplay = config.Source switch
        {
            ExtractionSource.JsonPath => "JSON",
            ExtractionSource.ResponseHeader => "Header",
            ExtractionSource.Regex => "Regex",
            _ => config.Source.ToString()
        };
        return $"[blue]Custom ({sourceDisplay})[/] {valueStatus}";
    }

    private static string FormatOAuth2Display(OAuth2Config config)
    {
        string grantName = config.GrantType switch
        {
            OAuth2GrantType.ClientCredentials => "Client Credentials",
            OAuth2GrantType.AuthorizationCode => "Authorization Code",
            OAuth2GrantType.ResourceOwnerPassword => "Password",
            _ => config.GrantType.ToString()
        };

        string tokenStatus = config.Token switch
        {
            null => "[grey]no token[/]",
            { IsExpired: true } => "[yellow]expired[/]",
            _ => "[green]valid[/]"
        };

        return $"[blue]OAuth 2.0 ({grantName})[/] {tokenStatus}";
    }

    internal static async Task<StraumrAuthConfig?> EditAuthAsync(
        EscapeCancellableConsole console, StraumrAuthConfig? auth)
    {
        StraumrAuthConfig? current = auth;
        while (true)
        {
            const string actionBack = "Back";
            const string actionType = "Auth type";
            const string actionConfig = "Configure";
            const string actionTokenStatus = "Token status";
            const string actionClear = "Clear auth";

            AuthType currentType = current?.Type ?? AuthType.None;
            string typeDisplay = currentType switch
            {
                AuthType.Bearer => "[blue]Bearer[/]",
                AuthType.Basic => "[blue]Basic[/]",
                AuthType.OAuth2 => "[blue]OAuth 2.0[/]",
                AuthType.Custom => "[blue]Custom[/]",
                _ => "[grey]none[/]"
            };

            string[] choices = current is null
                ? [actionBack, actionType]
                : [actionBack, actionType, actionConfig, actionTokenStatus, actionClear];

            SelectionPrompt<string> prompt = new SelectionPrompt<string>()
                .Title("Auth")
                .EnableSearch()
                .SearchPlaceholderText("/")
                .UseConverter(choice => choice switch
                {
                    actionType => $"Type: {typeDisplay}",
                    _ => choice
                })
                .AddChoices(choices);

            string? action = await PromptAsync(console, prompt);
            if (action is null || action == actionBack)
            {
                return current;
            }

            switch (action)
            {
                case actionType:
                    current = await SelectAuthTypeAsync(console, current);
                    break;
                case actionConfig:
                    await ConfigureAuthAsync(console, current);
                    break;
                case actionTokenStatus:
                    ShowAuthStatus(current);
                    break;
                case actionClear:
                    current = null;
                    break;
            }
        }
    }

    private static async Task<StraumrAuthConfig?> SelectAuthTypeAsync(
        EscapeCancellableConsole console, StraumrAuthConfig? current)
    {
        string? selected = await PromptMenuAsync(console, "Select auth type",
            ["No auth", "Bearer", "Basic", "OAuth 2.0", "Custom"]);

        return selected switch
        {
            "Bearer" => current as BearerAuthConfig ?? new BearerAuthConfig(),
            "Basic" => current as BasicAuthConfig ?? new BasicAuthConfig(),
            "OAuth 2.0" => current as OAuth2Config ?? new OAuth2Config(),
            "Custom" => current as CustomAuthConfig ?? new CustomAuthConfig(),
            "No auth" => null,
            _ => current
        };
    }

    private static async Task ConfigureAuthAsync(EscapeCancellableConsole console, StraumrAuthConfig? auth)
    {
        switch (auth)
        {
            case BearerAuthConfig bearer:
                await EditBearerAuthAsync(console, bearer);
                break;
            case BasicAuthConfig basic:
                await EditBasicAuthAsync(console, basic);
                break;
            case OAuth2Config oauth2:
                await EditOAuth2ConfigAsync(console, oauth2);
                break;
            case CustomAuthConfig custom:
                await EditCustomAuthConfigAsync(console, custom);
                break;
        }
    }

    private static void ShowAuthStatus(StraumrAuthConfig? auth)
    {
        switch (auth)
        {
            case BearerAuthConfig bearer when !string.IsNullOrWhiteSpace(bearer.Token):
                string masked = bearer.Token[..Math.Min(20, bearer.Token.Length)];
                ShowTransientMessage(
                    $"Prefix: [blue]{Markup.Escape(bearer.Prefix)}[/]\nToken: [blue]{Markup.Escape(masked)}...[/]");
                break;
            case BearerAuthConfig:
                ShowTransientMessage("[grey]No token set.[/]");
                break;
            case BasicAuthConfig basic when !string.IsNullOrWhiteSpace(basic.Username):
                ShowTransientMessage(
                    $"Username: [blue]{Markup.Escape(basic.Username)}[/]\nPassword: [blue]****[/]");
                break;
            case BasicAuthConfig:
                ShowTransientMessage("[grey]No credentials set.[/]");
                break;
            case OAuth2Config { Token: null }:
                ShowTransientMessage("[grey]No token fetched yet.[/]");
                break;
            case OAuth2Config { Token: { } token }:
            {
                string status = token.IsExpired ? "[yellow]Expired[/]" : "[green]Valid[/]";
                string expiresDisplay = token.ExpiresAt.HasValue
                    ? token.ExpiresAt.Value.ToString("yyyy-MM-dd HH:mm:ss UTC")
                    : "N/A";
                string hasRefresh = token.RefreshToken is not null ? "[green]Yes[/]" : "[grey]No[/]";
                ShowTransientMessage(
                    $"Status: {status}\n" +
                    $"Token type: [blue]{Markup.Escape(token.TokenType)}[/]\n" +
                    $"Expires: [blue]{expiresDisplay}[/]\n" +
                    $"Refresh token: {hasRefresh}");
                break;
            }
            case CustomAuthConfig { CachedValue: null }:
                ShowTransientMessage("[grey]No value fetched yet.[/]");
                break;
            case CustomAuthConfig custom:
            {
                string headerPreview = custom.ApplyHeaderTemplate.Replace("{{value}}", custom.CachedValue);
                ShowTransientMessage(
                    $"Cached value: [blue]{Markup.Escape(custom.CachedValue)}[/]\n" +
                    $"Applied as: [blue]{Markup.Escape(custom.ApplyHeaderName)}: {Markup.Escape(headerPreview)}[/]");
                break;
            }
        }
    }

    internal static async Task FetchAuthValueAsync(IStraumrAuthService authService, StraumrAuthConfig? auth)
    {
        if (auth is not (OAuth2Config or CustomAuthConfig))
        {
            return;
        }

        try
        {
            switch (auth)
            {
                case OAuth2Config oauth2:
                {
                    OAuth2Token token = await authService.FetchTokenAsync(oauth2);
                    oauth2.Token = token;
                    string expiresDisplay = token.ExpiresAt.HasValue
                        ? token.ExpiresAt.Value.ToString("yyyy-MM-dd HH:mm:ss UTC")
                        : "N/A";
                    ShowTransientMessage(
                        $"[green]Token fetched successfully![/]\n" +
                        $"Type: [blue]{Markup.Escape(token.TokenType)}[/]\n" +
                        $"Expires: [blue]{expiresDisplay}[/]");
                    break;
                }
                case CustomAuthConfig customAuth:
                {
                    string value = await authService.ExecuteCustomAuthAsync(customAuth);
                    string headerPreview = customAuth.ApplyHeaderTemplate.Replace("{{value}}", value);
                    ShowTransientMessage(
                        $"[green]Value fetched successfully![/]\n" +
                        $"Extracted: [blue]{Markup.Escape(value)}[/]\n" +
                        $"Header: [blue]{Markup.Escape(customAuth.ApplyHeaderName)}: {Markup.Escape(headerPreview)}[/]");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            ShowTransientMessage($"[red]Failed to fetch: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private static async Task EditOAuth2ConfigAsync(EscapeCancellableConsole console, OAuth2Config config)
    {
        while (true)
        {
            string grantDisplay = config.GrantType switch
            {
                OAuth2GrantType.ClientCredentials => "[blue]Client Credentials[/]",
                OAuth2GrantType.AuthorizationCode => "[blue]Authorization Code[/]",
                OAuth2GrantType.ResourceOwnerPassword => "[blue]Password[/]",
                _ => "[grey]unknown[/]"
            };

            const string actionBack = "Back";
            const string actionGrant = "Grant type";
            const string actionTokenUrl = "Token URL";
            const string actionClientId = "Client ID";
            const string actionClientSecret = "Client secret";
            const string actionScope = "Scope";
            const string actionAuthUrl = "Authorization URL";
            const string actionRedirectUri = "Redirect URI";
            const string actionPkce = "PKCE";
            const string actionUsername = "Username";
            const string actionPassword = "Password";

            string tokenUrlDisplay = string.IsNullOrWhiteSpace(config.TokenUrl)
                ? "[grey]not set[/]"
                : $"[blue]{Markup.Escape(config.TokenUrl)}[/]";
            string clientIdDisplay = string.IsNullOrWhiteSpace(config.ClientId)
                ? "[grey]not set[/]"
                : $"[blue]{Markup.Escape(config.ClientId)}[/]";
            string clientSecretDisplay = string.IsNullOrWhiteSpace(config.ClientSecret)
                ? "[grey]not set[/]"
                : "[blue]****[/]";
            string scopeDisplay = string.IsNullOrWhiteSpace(config.Scope)
                ? "[grey]not set[/]"
                : $"[blue]{Markup.Escape(config.Scope)}[/]";

            var choices = new List<string>
                { actionBack, actionGrant, actionTokenUrl, actionClientId, actionClientSecret, actionScope };

            if (config.GrantType == OAuth2GrantType.AuthorizationCode)
            {
                string authUrlDisplay = string.IsNullOrWhiteSpace(config.AuthorizationUrl)
                    ? "[grey]not set[/]"
                    : $"[blue]{Markup.Escape(config.AuthorizationUrl)}[/]";
                var redirectDisplay = $"[blue]{Markup.Escape(config.RedirectUri)}[/]";
                string pkceDisplay = config.UsePkce
                    ? $"[green]Enabled[/] ({config.CodeChallengeMethod})"
                    : "[grey]Disabled[/]";

                choices.AddRange([actionAuthUrl, actionRedirectUri, actionPkce]);

                SelectionPrompt<string> authCodePrompt = new SelectionPrompt<string>()
                    .Title("OAuth 2.0 Configuration")
                    .EnableSearch()
                    .SearchPlaceholderText("/")
                    .UseConverter(choice => choice switch
                    {
                        actionGrant => $"Grant type: {grantDisplay}",
                        actionTokenUrl => $"Token URL: {tokenUrlDisplay}",
                        actionClientId => $"Client ID: {clientIdDisplay}",
                        actionClientSecret => $"Client secret: {clientSecretDisplay}",
                        actionScope => $"Scope: {scopeDisplay}",
                        actionAuthUrl => $"Authorization URL: {authUrlDisplay}",
                        actionRedirectUri => $"Redirect URI: {redirectDisplay}",
                        actionPkce => $"PKCE: {pkceDisplay}",
                        _ => choice
                    })
                    .AddChoices(choices);

                string? action = await PromptAsync(console, authCodePrompt);
                if (action is null || action == actionBack)
                {
                    return;
                }

                await HandleOAuth2Action(console, config, action);
            }
            else if (config.GrantType == OAuth2GrantType.ResourceOwnerPassword)
            {
                string usernameDisplay = string.IsNullOrWhiteSpace(config.Username)
                    ? "[grey]not set[/]"
                    : $"[blue]{Markup.Escape(config.Username)}[/]";
                string passwordDisplay = string.IsNullOrWhiteSpace(config.Password)
                    ? "[grey]not set[/]"
                    : "[blue]****[/]";

                choices.AddRange([actionUsername, actionPassword]);

                SelectionPrompt<string> passwordPrompt = new SelectionPrompt<string>()
                    .Title("OAuth 2.0 Configuration")
                    .EnableSearch()
                    .SearchPlaceholderText("/")
                    .UseConverter(choice => choice switch
                    {
                        actionGrant => $"Grant type: {grantDisplay}",
                        actionTokenUrl => $"Token URL: {tokenUrlDisplay}",
                        actionClientId => $"Client ID: {clientIdDisplay}",
                        actionClientSecret => $"Client secret: {clientSecretDisplay}",
                        actionScope => $"Scope: {scopeDisplay}",
                        actionUsername => $"Username: {usernameDisplay}",
                        actionPassword => $"Password: {passwordDisplay}",
                        _ => choice
                    })
                    .AddChoices(choices);

                string? action = await PromptAsync(console, passwordPrompt);
                if (action is null || action == actionBack)
                {
                    return;
                }

                await HandleOAuth2Action(console, config, action);
            }
            else
            {
                SelectionPrompt<string> ccPrompt = new SelectionPrompt<string>()
                    .Title("OAuth 2.0 Configuration")
                    .EnableSearch()
                    .SearchPlaceholderText("/")
                    .UseConverter(choice => choice switch
                    {
                        actionGrant => $"Grant type: {grantDisplay}",
                        actionTokenUrl => $"Token URL: {tokenUrlDisplay}",
                        actionClientId => $"Client ID: {clientIdDisplay}",
                        actionClientSecret => $"Client secret: {clientSecretDisplay}",
                        actionScope => $"Scope: {scopeDisplay}",
                        _ => choice
                    })
                    .AddChoices(choices);

                string? action = await PromptAsync(console, ccPrompt);
                if (action is null || action == actionBack)
                {
                    return;
                }

                await HandleOAuth2Action(console, config, action);
            }
        }
    }

    private static async Task EditBearerAuthAsync(EscapeCancellableConsole console, BearerAuthConfig config)
    {
        while (true)
        {
            const string actionBack = "Back";
            const string actionToken = "Token";
            const string actionPrefix = "Prefix";

            string tokenDisplay = string.IsNullOrWhiteSpace(config.Token) ? "[grey]not set[/]" : "[blue]****[/]";
            var prefixDisplay = $"[blue]{Markup.Escape(config.Prefix)}[/]";

            SelectionPrompt<string> prompt = new SelectionPrompt<string>()
                .Title("Bearer Auth")
                .EnableSearch()
                .SearchPlaceholderText("/")
                .UseConverter(choice => choice switch
                {
                    actionToken => $"Token: {tokenDisplay}",
                    actionPrefix => $"Prefix: {prefixDisplay}",
                    _ => choice
                })
                .AddChoices(actionBack, actionToken, actionPrefix);

            string? action = await PromptAsync(console, prompt);
            if (action is null || action == actionBack)
            {
                return;
            }

            switch (action)
            {
                case actionToken:
                {
                    string? value = await PromptAsync(console, new TextPrompt<string>("Token").Secret());
                    if (value is not null)
                    {
                        config.Token = value;
                    }

                    break;
                }
                case actionPrefix:
                {
                    string? value = await PromptAsync(console,
                        new TextPrompt<string>("Prefix (e.g. Bearer, Token)")
                            .DefaultValue(config.Prefix));
                    if (value is not null)
                    {
                        config.Prefix = value;
                    }

                    break;
                }
            }
        }
    }

    private static async Task EditBasicAuthAsync(EscapeCancellableConsole console, BasicAuthConfig config)
    {
        while (true)
        {
            const string actionBack = "Back";
            const string actionUsername = "Username";
            const string actionPassword = "Password";

            string usernameDisplay = string.IsNullOrWhiteSpace(config.Username)
                ? "[grey]not set[/]"
                : $"[blue]{Markup.Escape(config.Username)}[/]";
            string passwordDisplay = string.IsNullOrWhiteSpace(config.Password) ? "[grey]not set[/]" : "[blue]****[/]";

            SelectionPrompt<string> prompt = new SelectionPrompt<string>()
                .Title("Basic Auth")
                .EnableSearch()
                .SearchPlaceholderText("/")
                .UseConverter(choice => choice switch
                {
                    actionUsername => $"Username: {usernameDisplay}",
                    actionPassword => $"Password: {passwordDisplay}",
                    _ => choice
                })
                .AddChoices(actionBack, actionUsername, actionPassword);

            string? action = await PromptAsync(console, prompt);
            if (action is null || action == actionBack)
            {
                return;
            }

            switch (action)
            {
                case actionUsername:
                {
                    string? value = await PromptAsync(console, new TextPrompt<string>("Username"));
                    if (value is not null)
                    {
                        config.Username = value;
                    }

                    break;
                }
                case actionPassword:
                {
                    string? value = await PromptAsync(console, new TextPrompt<string>("Password").Secret());
                    if (value is not null)
                    {
                        config.Password = value;
                    }

                    break;
                }
            }
        }
    }

    private static async Task EditCustomAuthConfigAsync(EscapeCancellableConsole console, CustomAuthConfig config)
    {
        while (true)
        {
            const string actionBack = "Back";
            const string actionUrl = "URL";
            const string actionMethod = "Method";
            const string actionHeaders = "Headers";
            const string actionParams = "Params";
            const string actionBody = "Body";
            const string actionSource = "Extraction source";
            const string actionExpression = "Extraction expression";
            const string actionHeaderName = "Apply header name";
            const string actionTemplate = "Apply header template";

            string urlDisplay = string.IsNullOrWhiteSpace(config.Url)
                ? "[grey]not set[/]"
                : $"[blue]{Markup.Escape(config.Url)}[/]";
            var methodDisplay = $"[blue]{config.Method}[/]";
            string headersDisplay = config.Headers.Count == 0 ? "[grey]none[/]" : $"[blue]{config.Headers.Count}[/]";
            string paramsDisplay = config.Params.Count == 0 ? "[grey]none[/]" : $"[blue]{config.Params.Count}[/]";
            string bodyDisplay = config.BodyType == BodyType.None
                ? "[grey]none[/]"
                : $"[blue]{BodyTypeDisplayName(config.BodyType)}[/]";

            string sourceDisplay = config.Source switch
            {
                ExtractionSource.JsonPath => "[blue]JSON path[/]",
                ExtractionSource.ResponseHeader => "[blue]Response header[/]",
                ExtractionSource.Regex => "[blue]Regex[/]",
                _ => "[grey]unknown[/]"
            };
            string expressionDisplay = string.IsNullOrWhiteSpace(config.ExtractionExpression)
                ? "[grey]not set[/]"
                : $"[blue]{Markup.Escape(config.ExtractionExpression)}[/]";
            var headerNameDisplay = $"[blue]{Markup.Escape(config.ApplyHeaderName)}[/]";
            var templateDisplay = $"[blue]{Markup.Escape(config.ApplyHeaderTemplate)}[/]";

            SelectionPrompt<string> prompt = new SelectionPrompt<string>()
                .Title("Custom Auth Configuration")
                .EnableSearch()
                .SearchPlaceholderText("/")
                .UseConverter(choice => choice switch
                {
                    actionUrl => $"URL: {urlDisplay}",
                    actionMethod => $"Method: {methodDisplay}",
                    actionHeaders => $"Headers: {headersDisplay}",
                    actionParams => $"Params: {paramsDisplay}",
                    actionBody => $"Body: {bodyDisplay}",
                    actionSource => $"Extract from: {sourceDisplay}",
                    actionExpression => $"Expression: {expressionDisplay}",
                    actionHeaderName => $"Apply header: {headerNameDisplay}",
                    actionTemplate => $"Template: {templateDisplay}",
                    _ => choice
                })
                .AddChoices(actionBack, actionUrl, actionMethod, actionHeaders, actionParams, actionBody,
                    actionSource, actionExpression, actionHeaderName, actionTemplate);

            string? action = await PromptAsync(console, prompt);
            if (action is null || action == actionBack)
            {
                return;
            }

            switch (action)
            {
                case actionUrl:
                {
                    string? value = await PromptAsync(console,
                        new TextPrompt<string>("Auth request URL")
                            .Validate(v => Uri.TryCreate(v, UriKind.Absolute, out _)
                                ? ValidationResult.Success()
                                : ValidationResult.Error("Please enter a valid absolute URL.")));
                    if (value is not null)
                    {
                        config.Url = value;
                    }

                    break;
                }
                case actionMethod:
                {
                    string? selected = await PromptMethodAsync(console);
                    if (selected is not null)
                    {
                        config.Method = selected;
                    }

                    break;
                }
                case actionHeaders:
                    await EditKeyValuePairsAsync(console, "Auth headers", config.Headers);
                    break;
                case actionParams:
                    await EditKeyValuePairsAsync(console, "Auth params", config.Params);
                    break;
                case actionBody:
                    config.BodyType = await EditBodyAsync(console, config.Headers, config.Bodies, config.BodyType,
                        CancellationToken.None);
                    break;
                case actionSource:
                {
                    string? selected = await PromptMenuAsync(console, "Extract value from",
                        ["JSON path (dot notation)", "Response header", "Regex (first capture group)"]);

                    if (selected is not null)
                    {
                        config.Source = selected switch
                        {
                            "JSON path (dot notation)" => ExtractionSource.JsonPath,
                            "Response header" => ExtractionSource.ResponseHeader,
                            "Regex (first capture group)" => ExtractionSource.Regex,
                            _ => config.Source
                        };
                    }

                    break;
                }
                case actionExpression:
                {
                    string hint = config.Source switch
                    {
                        ExtractionSource.JsonPath => "JSON path (e.g. access_token or data.token)",
                        ExtractionSource.ResponseHeader => "Header name (e.g. X-Auth-Token)",
                        ExtractionSource.Regex => "Regex with capture group (e.g. token\":\"([^\"]+)\")",
                        _ => "Expression"
                    };
                    string? value = await PromptAsync(console,
                        new TextPrompt<string>(hint)
                            .Validate(v => string.IsNullOrWhiteSpace(v)
                                ? ValidationResult.Error("Expression cannot be empty.")
                                : ValidationResult.Success()));
                    if (value is not null)
                    {
                        config.ExtractionExpression = value;
                    }

                    break;
                }
                case actionHeaderName:
                {
                    string? value = await PromptAsync(console,
                        new TextPrompt<string>("Header name (e.g. Authorization)")
                            .DefaultValue(config.ApplyHeaderName)
                            .Validate(v => string.IsNullOrWhiteSpace(v)
                                ? ValidationResult.Error("Header name cannot be empty.")
                                : ValidationResult.Success()));
                    if (value is not null)
                    {
                        config.ApplyHeaderName = value;
                    }

                    break;
                }
                case actionTemplate:
                {
                    string? value = await PromptAsync(console,
                        new TextPrompt<string>("Header value template (use {{value}} as placeholder)")
                            .DefaultValue(config.ApplyHeaderTemplate)
                            .Validate(v => string.IsNullOrWhiteSpace(v)
                                ? ValidationResult.Error("Template cannot be empty.")
                                : ValidationResult.Success()));
                    if (value is not null)
                    {
                        config.ApplyHeaderTemplate = value;
                    }

                    break;
                }
            }
        }
    }

    private static async Task HandleOAuth2Action(EscapeCancellableConsole console, OAuth2Config config, string action)
    {
        switch (action)
        {
            case "Grant type":
            {
                string? selected = await PromptMenuAsync(console, "Select grant type",
                    ["Client Credentials", "Authorization Code", "Resource Owner Password"]);

                if (selected is not null)
                {
                    config.GrantType = selected switch
                    {
                        "Client Credentials" => OAuth2GrantType.ClientCredentials,
                        "Authorization Code" => OAuth2GrantType.AuthorizationCode,
                        "Resource Owner Password" => OAuth2GrantType.ResourceOwnerPassword,
                        _ => config.GrantType
                    };
                }

                break;
            }
            case "Token URL":
            {
                string? value = await PromptAsync(console,
                    new TextPrompt<string>("Token URL")
                        .Validate(v => Uri.TryCreate(v, UriKind.Absolute, out _)
                            ? ValidationResult.Success()
                            : ValidationResult.Error("Please enter a valid absolute URL.")));
                if (value is not null)
                {
                    config.TokenUrl = value;
                }

                break;
            }
            case "Client ID":
            {
                string? value = await PromptAsync(console, new TextPrompt<string>("Client ID"));
                if (value is not null)
                {
                    config.ClientId = value;
                }

                break;
            }
            case "Client secret":
            {
                string? value = await PromptAsync(console, new TextPrompt<string>("Client secret").Secret());
                if (value is not null)
                {
                    config.ClientSecret = value;
                }

                break;
            }
            case "Scope":
            {
                string? value = await PromptAsync(console, new TextPrompt<string>("Scope").AllowEmpty());
                if (value is not null)
                {
                    config.Scope = value;
                }

                break;
            }
            case "Authorization URL":
            {
                string? value = await PromptAsync(console,
                    new TextPrompt<string>("Authorization URL")
                        .Validate(v => Uri.TryCreate(v, UriKind.Absolute, out _)
                            ? ValidationResult.Success()
                            : ValidationResult.Error("Please enter a valid absolute URL.")));
                if (value is not null)
                {
                    config.AuthorizationUrl = value;
                }

                break;
            }
            case "Redirect URI":
            {
                string? value = await PromptAsync(console,
                    new TextPrompt<string>("Redirect URI")
                        .DefaultValue(config.RedirectUri)
                        .Validate(v => Uri.TryCreate(v, UriKind.Absolute, out _)
                            ? ValidationResult.Success()
                            : ValidationResult.Error("Please enter a valid absolute URL.")));
                if (value is not null)
                {
                    config.RedirectUri = value;
                }

                break;
            }
            case "PKCE":
            {
                string? selected = await PromptMenuAsync(console, "PKCE",
                    ["Disabled", "S256 (recommended)", "plain"]);

                if (selected is not null)
                {
                    switch (selected)
                    {
                        case "Disabled":
                            config.UsePkce = false;
                            break;
                        case "S256 (recommended)":
                            config.UsePkce = true;
                            config.CodeChallengeMethod = "S256";
                            break;
                        case "plain":
                            config.UsePkce = true;
                            config.CodeChallengeMethod = "plain";
                            break;
                    }
                }

                break;
            }
            case "Username":
            {
                string? value = await PromptAsync(console, new TextPrompt<string>("Username"));
                if (value is not null)
                {
                    config.Username = value;
                }

                break;
            }
            case "Password":
            {
                string? value = await PromptAsync(console, new TextPrompt<string>("Password").Secret());
                if (value is not null)
                {
                    config.Password = value;
                }

                break;
            }
        }
    }
}