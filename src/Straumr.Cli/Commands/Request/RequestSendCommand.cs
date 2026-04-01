using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Request;

public class RequestSendCommand(IStraumrRequestService requestService)
    : AsyncCommand<RequestSendCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        try
        {
            StraumrRequest request = await requestService.GetAsync(settings.Identifier);

            var options = new SendOptions
            {
                Insecure = settings.Insecure,
                FollowRedirects = settings.FollowRedirects
            };

            StraumrResponse response = await requestService.SendAsync(request, options);
            string content = settings.Beautify ? BeautifyContent(response.Content) : response.Content ?? string.Empty;

            if (settings.Verbose)
            {
                if (settings.Pretty)
                {
                    RenderPrettyVerbose(response);
                }
                else
                {
                    RenderVerbose(response);
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

            if (settings.IncludeHeaders)
            {
                RenderIncludeHeaders(response);
            }

            if (settings.Pretty)
            {
                RenderPrettyBody(response, settings.Beautify);
                RenderPrettySummary(request, response);
            }
            else if (!string.IsNullOrEmpty(settings.OutputFile))
            {
                await File.WriteAllTextAsync(settings.OutputFile, content, cancellation);
            }
            else
            {
                System.Console.Write(content);
            }

            if (settings.Fail && response.StatusCode is not null && (int)response.StatusCode.Value >= 400)
            {
                if (!settings.Silent)
                {
                    await System.Console.Error.WriteLineAsync(
                        $"The requested URL returned error: {(int)response.StatusCode.Value} {response.ReasonPhrase}");
                }

                return 22;
            }

            return 0;
        }
        catch (StraumrException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return ex.Reason == StraumrError.MissingEntry ? 1 : -1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return -1;
        }
    }

    private static void RenderVerbose(StraumrResponse response)
    {
        if (response.RequestLine is not null)
        {
            System.Console.Error.WriteLine($"> {response.RequestLine}");
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in response.RequestHeaders)
        {
            System.Console.Error.WriteLine($"> {header.Key}: {string.Join(", ", header.Value)}");
        }

        System.Console.Error.WriteLine(">");

        if (response.StatusCode is not null)
        {
            System.Console.Error.WriteLine(
                $"< HTTP/{response.HttpVersion} {(int)response.StatusCode.Value} {response.ReasonPhrase}");
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in response.ResponseHeaders)
        {
            System.Console.Error.WriteLine($"< {header.Key}: {string.Join(", ", header.Value)}");
        }

        System.Console.Error.WriteLine("<");
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

    private static void RenderPrettySummary(StraumrRequest request, StraumrResponse response)
    {
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .Expand();

        table.AddColumn(new TableColumn("[bold]Request[/]").NoWrap());
        table.AddColumn(new TableColumn("[bold]Response[/]").NoWrap());

        int contentLength = response.Content?.Length ?? 0;

        int byteLength = response.Content is null ? 0 : Encoding.UTF8.GetByteCount(response.Content);

        string authInfo = FormatRequestAuthInfo(request.Auth);

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
            .BorderColor(Color.Grey);

        AnsiConsole.Write(panel);
    }

    private static void RenderPrettyVerbose(StraumrResponse response)
    {
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow("[bold]Request[/]", "[bold]Response[/]");

        var requestLines = new StringBuilder();
        if (response.RequestLine is not null)
        {
            requestLines.AppendLine($"[blue]{Markup.Escape(response.RequestLine)}[/]");
        }

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

        grid.AddRow(
            new Markup(requestLines.ToString().TrimEnd()),
            new Markup(responseLines.ToString().TrimEnd()));

        Panel panel = new Panel(grid)
            .Header("Details", Justify.Left)
            .BorderColor(Color.Blue);

        AnsiConsole.Write(panel);
    }

    private static string BeautifyContent(string? content)
    {
        if (content is null)
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(content);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                doc.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException) { }

        try
        {
            XDocument doc = XDocument.Parse(content);
            return doc.ToString();
        }
        catch (System.Xml.XmlException) { }

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

        var code = (int)response.StatusCode.Value;
        var name = response.StatusCode.Value.ToString();
        string color = code switch
        {
            >= 200 and < 300 => "green",
            >= 300 and < 400 => "yellow",
            >= 400 and < 500 => "yellow",
            >= 500 => "red",
            _ => "white"
        };

        return $"[{color}]{code} {name}[/]";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    private static string FormatRequestAuthInfo(StraumrAuthConfig? auth)
    {
        return auth switch
        {
            BearerAuthConfig => "\nAuth: [blue]Bearer[/]",
            BasicAuthConfig => "\nAuth: [blue]Basic[/]",
            OAuth2Config { Token: { } token } =>
                $"\nAuth: [blue]OAuth 2.0[/] ({(token.IsExpired ? "[yellow]expired[/]" : "[green]valid[/]")})",
            CustomAuthConfig => "\nAuth: [blue]Custom[/]",
            _ => string.Empty
        };
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")] public required string Identifier { get; set; }
        [CommandOption("-v")] public bool Verbose { get; set; }
        [CommandOption("-p|--pretty")] public bool Pretty { get; set; }
        [CommandOption("-b|--beautify")] public bool Beautify { get; set; }
        [CommandOption("-k|--insecure")] public bool Insecure { get; set; }
        [CommandOption("-L|--location")] public bool FollowRedirects { get; set; }
        [CommandOption("-o|--output")] public string? OutputFile { get; set; }
        [CommandOption("-f|--fail")] public bool Fail { get; set; }
        [CommandOption("-i|--include")] public bool IncludeHeaders { get; set; }
        [CommandOption("-s|--silent")] public bool Silent { get; set; }
    }
}