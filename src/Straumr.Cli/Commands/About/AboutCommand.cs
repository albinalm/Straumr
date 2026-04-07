using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Straumr.Cli.Commands.About;

public sealed class AboutCommand : AsyncCommand
{
    private const string RepoUrl = "github.com/albinalm/Straumr";
    private const string ReadmeUrl = "https://raw.githubusercontent.com/albinalm/Straumr/main/README.md";
    private const string AboutStart = "<!-- ABOUT:START -->";
    private const string AboutEnd = "<!-- ABOUT:END -->";
    private readonly HttpClient _httpClient;

    public AboutCommand(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(3);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        string? aboutText = await TryFetchAboutFromReadmeAsync(cancellationToken);
        Render(aboutText);
        return 0;
    }

    private static void Render(string? aboutText)
    {
        AnsiConsole.Write(new FigletText("Straumr").Color(Color.Green));
        AnsiConsole.WriteLine();

        Assembly assembly = typeof(Program).Assembly;
        string version = assembly.GetName().Version?.ToString() ?? "unknown";

        string body = string.IsNullOrWhiteSpace(aboutText)
            ? "Straumr is a CLI tool for managing and sending HTTP requests."
            : aboutText.Trim();

        Panel aboutPanel = new Panel(new Markup($"{Markup.Escape(body)}"))
            .Expand();

        AnsiConsole.Write(aboutPanel);
        
        Panel metadataPanel =
            new Panel(new Markup(
                    $"[bold]Version: [/]{Markup.Escape(version)}\n[bold]Repository: [/]{Markup.Escape(RepoUrl)}"))
                .Expand();
        
        AnsiConsole.Write(metadataPanel);
        AnsiConsole.WriteLine();
    }

    private async Task<string?> TryFetchAboutFromReadmeAsync(CancellationToken cancellationToken)
    {
        try
        {
            string readme = await _httpClient.GetStringAsync(ReadmeUrl, cancellationToken);
            return ExtractBetweenMarkers(readme);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractBetweenMarkers(string source)
    {
        int start = source.IndexOf(AboutStart, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += AboutStart.Length;
        int end = source.IndexOf(AboutEnd, start, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        string segment = source[start..end].Trim();
        return string.IsNullOrWhiteSpace(segment) ? null : segment;
    }
}