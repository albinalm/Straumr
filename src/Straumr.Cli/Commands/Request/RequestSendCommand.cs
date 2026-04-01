using System.Text;
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
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")] public required string Identifier { get; set; }
        [CommandOption("-v|--verbose")] public bool Verbose { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        try
        {
            StraumrRequest request = await requestService.GetAsync(settings.Identifier);
            StraumrResponse response = await requestService.SendAsync(request);

            if (settings.Verbose)
            {
                RenderVerboseBody(request, response);
            }
            else
            {
                RenderNormalBody(response);
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

    private static void RenderNormalBody(StraumrResponse response)
    {
        if (response.Exception is not null)
        {
            AnsiConsole.Markup($"[red]{Markup.Escape(response.Exception.Message)}[/]");
        }
        else
        {
            System.Console.Write(response.Content);
        }
    }
    private static void RenderVerboseBody(StraumrRequest request, StraumrResponse response)
    {
        RenderBody(response);
        RenderSummary(request, response);
    }
    private static void RenderSummary(StraumrRequest request, StraumrResponse response)
    {
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .Expand();

        table.AddColumn(new TableColumn("[bold]Request[/]").NoWrap());
        table.AddColumn(new TableColumn("[bold]Response[/]").NoWrap());

        int contentLength = response.Content?.Length ?? 0;

        int byteLength = response.Content is null ? 0 : Encoding.UTF8.GetByteCount(response.Content);

        table.AddRow(
            $"[bold]{Markup.Escape(request.Name)}[/]\n[blue]{Markup.Escape(request.Method.Method)}[/] {Markup.Escape(request.Uri.ToString())}",
            $"Status: {FormatStatus(response)}\nDuration: [bold]{response.Duration.TotalMilliseconds:F0} ms[/]\nBody: [bold]{contentLength:N0} chars[/] ({FormatSize(byteLength)})");

        AnsiConsole.Write(table);
    }

    private static void RenderBody(StraumrResponse response)
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

        Panel panel = new Panel(new Markup(Markup.Escape(response.Content!)))
            .Header("Body", Justify.Left)
            .BorderColor(Color.Grey);

        AnsiConsole.Write(panel);
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
        string name = response.StatusCode.Value.ToString();
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
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }
}
