using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Request;

public class RequestSendCommand(
    IStraumrStateService stateService,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService,
    IStraumrAuthService authService)
    : AsyncCommand<RequestSendCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, RequestSendCommandSettings settings,
        CancellationToken cancellation)
    {
        StraumrWorkspaceEntry? workspaceEntry = stateService.State.CurrentWorkspace;

        if (settings.Workspace is not null)
        {
            StraumrWorkspaceEntry? resolved =
                await ResolveWorkspaceEntryAsync(settings.Workspace, workspaceService);
            if (resolved is null)
            {
                WriteError($"Workspace not found: {settings.Workspace}", settings.Json);
                return 1;
            }

            workspaceEntry = resolved;
        }

        if (workspaceEntry is null)
        {
            WriteError("No workspace loaded. Please load a workspace using 'workspace use <name>'", settings.Json);
            return 1;
        }

        try
        {
            StraumrRequest request = await GetRequestAsync(
                requestService, workspaceEntry, settings.Identifier, cancellationToken: cancellation);

            ApplyOverrides(request, settings.SendHeaders, settings.SendParams);

            StraumrAuth? auth = request.AuthId.HasValue
                ? await authService.GetAsync(workspaceEntry, request.AuthId.Value,
                    cancellationToken: cancellation)
                : null;

            if (settings.DryRun)
            {
                return await ExecuteDryRunAsync(request, auth, settings);
            }

            var options = new SendOptions
            {
                Insecure = settings.Insecure,
                FollowRedirects = settings.FollowRedirects
            };

            StraumrResponse response = await requestService.SendAsync(
                workspaceEntry, request, options, cancellation);

            if (settings.Json)
            {
                return OutputJsonResponse(response, settings);
            }

            string content = settings.Beautify ? BeautifyContent(response.Content) : response.Content ?? string.Empty;

            if (settings.Pretty && !settings.Verbose)
            {
                RenderPrettySummary(request, auth, response);
            }

            if (settings.Verbose)
            {
                if (settings.Pretty)
                {
                    RenderPrettyVerbose(request, auth, response);
                }
                else
                {
                    RenderVerbose(request, response);
                }
            }

            if (response.Exception is not null)
            {
                if (!settings.Silent)
                {
                    await System.Console.Error.WriteLineAsync(response.Exception.Message);
                }

                return 1;
            }

            if (settings.ResponseStatus)
            {
                System.Console.WriteLine(response.StatusCode.HasValue ? (int)response.StatusCode.Value : 0);
                return ExitCode(response, settings);
            }

            if (settings.ResponseHeaders)
            {
                RenderIncludeHeaders(response);
                return ExitCode(response, settings);
            }

            if (settings.IncludeHeaders)
            {
                RenderIncludeHeaders(response);
            }

            if (settings.Pretty)
            {
                RenderPrettyBody(response, settings.Beautify);
            }
            else if (!string.IsNullOrEmpty(settings.OutputFile))
            {
                await File.WriteAllTextAsync(settings.OutputFile, content, cancellation);
            }
            else
            {
                System.Console.Write(content);
            }

            if (!settings.Silent)
            {
                foreach (string warning in response.Warnings)
                {
                    AnsiConsole.MarkupLine($"\n[yellow]Warning:[/] {Markup.Escape(warning)}");
                }
            }

            return ExitCode(response, settings);
        }
        catch (StraumrException ex)
        {
            WriteError(ex.Message, settings.Json);
            return ex.Reason is StraumrError.MissingEntry or StraumrError.EntryNotFound ? 1 : -1;
        }
        catch (Exception ex)
        {
            WriteError(ex.Message, settings.Json);
            return -1;
        }
    }

    private static void ApplyOverrides(StraumrRequest request, string[]? headers, string[]? @params)
    {
        foreach (string header in headers ?? [])
        {
            int colon = header.IndexOf(':');
            if (colon < 0)
            {
                continue;
            }

            request.Headers[header[..colon].Trim()] = header[(colon + 1)..].Trim();
        }

        foreach (string param in @params ?? [])
        {
            int eq = param.IndexOf('=');
            if (eq < 0)
            {
                continue;
            }

            request.Params[param[..eq]] = param[(eq + 1)..];
        }
    }

    private async Task<int> ExecuteDryRunAsync(StraumrRequest request, StraumrAuth? auth, RequestSendCommandSettings settings)
    {
        (string resolvedUrl, IReadOnlyList<string> warnings) = await requestService.ResolveUrlAsync(request);

        if (settings.Json)
        {
            string? bodyContent = request.Bodies.TryGetValue(request.BodyType, out string? b) ? b : null;
            var result = new DryRunResult(
                request.Method.Method.ToUpperInvariant(),
                resolvedUrl,
                auth is not null ? $"{auth.Name} ({AuthTypeName(auth.Config)})" : null,
                request.Headers,
                request.Params,
                request.BodyType.ToString(),
                bodyContent
            );
            System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.DryRunResult));
            return 0;
        }

        AnsiConsole.MarkupLine("[grey]Dry run - request will not be sent[/]");
        AnsiConsole.MarkupLine($"[blue]{request.Method.Method.ToUpperInvariant()}[/] {Markup.Escape(resolvedUrl)}");

        if (auth is not null)
        {
            AnsiConsole.MarkupLine($"[grey]Auth:[/] {Markup.Escape(auth.Name)} ({Markup.Escape(AuthTypeName(auth.Config))})");
        }

        foreach (KeyValuePair<string, string> header in request.Headers)
        {
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(header.Key)}:[/] {Markup.Escape(header.Value)}");
        }

        if (request.Params.Count > 0)
        {
            AnsiConsole.MarkupLine("[grey]Query params:[/]");
            foreach (KeyValuePair<string, string> param in request.Params)
            {
                AnsiConsole.MarkupLine($"  {Markup.Escape(param.Key)}={Markup.Escape(param.Value)}");
            }
        }

        if (request.BodyType != BodyType.None && request.Bodies.TryGetValue(request.BodyType, out string? body))
        {
            AnsiConsole.MarkupLine($"[grey]Body ({request.BodyType}):[/]");
            System.Console.WriteLine(body);
        }

        foreach (string warning in warnings)
        {
            AnsiConsole.MarkupLine($"\n[yellow]Warning:[/] {Markup.Escape(warning)}");
        }

        return 0;
    }

    private static int OutputJsonResponse(StraumrResponse response, RequestSendCommandSettings settings)
    {
        if (response.Exception is not null)
        {
            var envelope = new CliErrorMessage(new CliErrorMessageContent(response.Exception.Message));
            System.Console.WriteLine(JsonSerializer.Serialize(envelope, CliJsonContext.Relaxed.CliErrorMessage));
            return 1;
        }

        Dictionary<string, string[]> headers = response.ResponseHeaders.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToArray());

        var result = new SendResult(
            response.StatusCode.HasValue ? (int)response.StatusCode.Value : null,
            response.ReasonPhrase,
            response.HttpVersion?.ToString(),
            response.Duration.TotalMilliseconds,
            headers,
            ParseBodyElement(response.Content, headers)
        );

        System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.SendResult));

        if (settings.Fail && response.StatusCode.HasValue && (int)response.StatusCode.Value >= 400)
        {
            return 22;
        }

        return 0;
    }

    private static JsonElement? ParseBodyElement(string? content, Dictionary<string, string[]> responseHeaders)
    {
        if (content is null)
        {
            return null;
        }

        bool isJson = responseHeaders.Any(h =>
            h.Key.Equals("content-type", StringComparison.OrdinalIgnoreCase) &&
            h.Value.Any(v => v.Contains("json", StringComparison.OrdinalIgnoreCase)));

        if (isJson)
        {
            try
            {
                return JsonSerializer.Deserialize(content, CliJsonContext.Relaxed.JsonElement);
            }
            catch
            {
            }
        }

        string serialized = JsonSerializer.Serialize(content, CliJsonContext.Relaxed.String);
        return JsonSerializer.Deserialize(serialized, CliJsonContext.Relaxed.JsonElement);
    }

    private static int ExitCode(StraumrResponse response, RequestSendCommandSettings settings)
    {
        if (settings.Fail && response.StatusCode is not null && (int)response.StatusCode.Value >= 400)
        {
            if (!settings.Silent)
            {
                System.Console.Error.WriteLine(
                    $"The requested URL returned error: {(int)response.StatusCode.Value} {response.ReasonPhrase}");
            }

            return 22;
        }

        return 0;
    }


    private static void RenderVerbose(StraumrRequest request, StraumrResponse response)
    {
        string requestLine =
            $"{request.Method.ToString().ToUpperInvariant()} {request.Uri} HTTP/{response.HttpVersion?.ToString() ?? "Unknown"}";

        System.Console.Error.WriteLine(requestLine);

        foreach (KeyValuePair<string, IEnumerable<string>> header in response.RequestHeaders)
        {
            System.Console.Error.WriteLine($"{header.Key}: {string.Join(", ", header.Value)}");
        }

        System.Console.Error.WriteLine();

        if (response.StatusCode is not null)
        {
            System.Console.Error.WriteLine(
                $"HTTP/{response.HttpVersion} {(int)response.StatusCode.Value} {response.ReasonPhrase}");
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in response.ResponseHeaders)
        {
            System.Console.Error.WriteLine($"{header.Key}: {string.Join(", ", header.Value)}");
        }

        System.Console.Error.WriteLine();

        System.Console.Error.WriteLine($"Duration: {response.Duration.TotalMilliseconds:F0} ms");

        int contentLength = response.Content?.Length ?? 0;
        int byteLength = response.Content is null ? 0 : Encoding.UTF8.GetByteCount(response.Content);
        System.Console.Error.WriteLine($"Body: {contentLength:N0} chars ({FormatSize(byteLength)})");
    }

    private static void RenderIncludeHeaders(StraumrResponse response)
    {
        if (response.StatusCode is not null)
        {
            System.Console.WriteLine(
                $"HTTP/{response.HttpVersion} {(int)response.StatusCode.Value} {response.ReasonPhrase}");
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in response.ResponseHeaders)
        {
            System.Console.WriteLine($"{header.Key}: {string.Join(", ", header.Value)}");
        }

        System.Console.WriteLine();
    }

    private static void RenderPrettySummary(StraumrRequest request, StraumrAuth? auth, StraumrResponse response)
    {
        Table table = new Table()
            .Border(TableBorder.Square)
            .BorderColor(Color.Blue)
            .Expand();

        table.AddColumn(new TableColumn("[bold]Request[/]").NoWrap());
        table.AddColumn(new TableColumn("[bold]Response[/]").NoWrap());

        int contentLength = response.Content?.Length ?? 0;

        int byteLength = response.Content is null ? 0 : Encoding.UTF8.GetByteCount(response.Content);

        string authInfo = auth is not null ? $"\nAuth: {FormatRequestAuthInfo(auth)}" : string.Empty;

        table.AddRow(
            $"Name: [bold]{Markup.Escape(request.Name)}[/]\nMethod: [blue]{Markup.Escape(request.Method.Method)}[/] {Markup.Escape(request.Uri)}{authInfo}",
            $"Status: {FormatStatus(response)}\nDuration: [bold]{response.Duration.TotalMilliseconds:F0} ms[/]\nBody: [bold]{contentLength:N0} chars[/] ({FormatSize(byteLength)})");

        AnsiConsole.Write(table);
    }

    private static void RenderPrettyBody(StraumrResponse response, bool beautify)
    {
        if (response.Exception is not null)
        {
            Panel errorPanel = new Panel($"[red]{Markup.Escape(response.Exception.Message)}[/]")
                .Header("Error", Justify.Left)
                .BorderColor(Color.Red);
            AnsiConsole.Write(errorPanel);
            return;
        }

        if (string.IsNullOrWhiteSpace(response.Content))
        {
            AnsiConsole.MarkupLine("[grey]No content[/]");
            return;
        }

        string content = beautify ? BeautifyContent(response.Content) : response.Content;

        Panel panel = new Panel(new Markup(Markup.Escape(content)))
            .Header("Body", Justify.Left)
            .BorderColor(Color.Grey)
            .Expand();

        AnsiConsole.Write(panel);
    }

    private static void RenderPrettyVerbose(StraumrRequest request, StraumrAuth? auth, StraumrResponse response)
    {
        Table table = new Table()
            .Border(TableBorder.Square)
            .BorderColor(Color.Blue)
            .Expand();
        table.AddColumn("[bold]Request[/]");
        table.AddColumn("[bold]Response[/]");

        var requestLines = new StringBuilder();
        requestLines.AppendLine($"Name: {request.Name}");
        if (auth is not null)
        {
            requestLines.AppendLine($"Auth: {FormatRequestAuthInfo(auth)}");
        }
        requestLines.AppendLine();

        requestLines.AppendLine(
            $"[blue]{Markup.Escape(request.Method.ToString().ToUpperInvariant())}[/] {Markup.Escape(request.Uri)} HTTP/{response.HttpVersion?.ToString() ?? "Unknown"}");

        foreach (KeyValuePair<string, IEnumerable<string>> header in response.RequestHeaders)
        {
            requestLines.AppendLine(
                $"[grey]{Markup.Escape(header.Key)}:[/] {Markup.Escape(string.Join(", ", header.Value))}");
        }

        var responseLines = new StringBuilder();
        if (response.StatusCode is not null)
        {
            responseLines.AppendLine(
                $"{FormatStatus(response)} [grey]HTTP/{response.HttpVersion}[/]");
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in response.ResponseHeaders)
        {
            responseLines.AppendLine(
                $"[grey]{Markup.Escape(header.Key)}:[/] {Markup.Escape(string.Join(", ", header.Value))}");
        }

        responseLines.AppendLine($"[grey]Duration:[/] [bold]{response.Duration.TotalMilliseconds:F0} ms[/]");

        int contentLength = response.Content?.Length ?? 0;
        int byteLength = response.Content is null ? 0 : Encoding.UTF8.GetByteCount(response.Content);
        responseLines.AppendLine(
            $"[grey]Body:[/] [bold]{contentLength:N0} chars[/] ({Markup.Escape(FormatSize(byteLength))})");

        table.AddRow(
            new Markup(requestLines.ToString().TrimEnd()),
            new Markup(responseLines.ToString().TrimEnd()));

        AnsiConsole.Write(table);
    }

    private static string BeautifyContent(string? content)
    {
        if (content is null)
        {
            return string.Empty;
        }

        if (RequestEditingHelpers.TryFormatJson(content, true, out string? formatted))
        {
            return formatted;
        }

        try
        {
            XDocument doc = XDocument.Parse(content);
            return doc.ToString();
        }
        catch (XmlException) { }

        return content;
    }

    private static string FormatStatus(StraumrResponse response)
    {
        if (response.Exception is not null)
        {
            return "[red]Error[/]";
        }

        if (response.StatusCode is null)
        {
            return "[yellow]N/A[/]";
        }

        int code = (int)response.StatusCode.Value;
        string reason = string.IsNullOrWhiteSpace(response.ReasonPhrase)
            ? response.StatusCode.Value.ToString()
            : response.ReasonPhrase!;
        string color = code switch
        {
            >= 200 and < 300 => "green",
            >= 300 and < 400 => "yellow",
            >= 400 and < 500 => "yellow",
            >= 500 => "red",
            _ => "white"
        };

        return $"[{color}]{code} {Markup.Escape(reason)}[/]";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    private static string FormatRequestAuthInfo(StraumrAuth auth)
    {
        return auth.Config switch
        {
            BearerAuthConfig => $"[blue]{Markup.Escape(auth.Name)}[/] (Bearer)",
            BasicAuthConfig => $"[blue]{Markup.Escape(auth.Name)}[/] (Basic)",
            OAuth2Config { Token: { } token } =>
                $"[blue]{Markup.Escape(auth.Name)}[/] (OAuth 2.0 {(token.IsExpired ? "[yellow]expired[/]" : "[green]valid[/]")})",
            CustomAuthConfig => $"[blue]{Markup.Escape(auth.Name)}[/] (Custom)",
            _ => Markup.Escape(auth.Name)
        };
    }
}
