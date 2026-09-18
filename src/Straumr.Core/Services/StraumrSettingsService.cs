using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Helpers;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Tomlyn;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace Straumr.Core.Services;

public class StraumrSettingsService : IStraumrSettingsService
{
    private static readonly string StraumrDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".straumr");

    public string SettingsPath { get; } = Path.Combine(StraumrDir, "settings.toml");

    public string SettingsDirectory => StraumrDir;

    public string? DefaultWorkspacePath => StraumrPaths.ExpandOrNull(Settings.Paths.Workspaces);

    public string DefaultSecretPath =>
        StraumrPaths.ExpandOrNull(Settings.Paths.Secrets) ?? Path.Combine(StraumrDir, "secrets");

    public StraumrSettings Settings { get; private set; } = new();

    public ResponseBodyFormat ResponseBodyFormat { get; private set; }

    public string? Problem { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Problem = null;
        ResponseBodyFormat = ResponseBodyFormat.None;
        string path = await EnsureFileAsync(cancellationToken);
        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Settings = new StraumrSettings();
            Problem = $"settings could not be read: {exception.Message}";
            return;
        }

        try
        {
            Settings = TomlSerializer.Deserialize(text, StraumrTomlContext.Default.StraumrSettings)
                       ?? new StraumrSettings();
        }
        catch (TomlException exception)
        {
            // The defaults stand and the caller reports the problem. Refusing to start over a typo
            // in a file whose whole purpose is to be typed in by hand would be the wrong trade.
            Settings = new StraumrSettings();
            Problem = $"settings.toml: {FirstLine(exception.Message)}";
            return;
        }

        if (Settings.Response.Format?.Trim() is not { Length: > 0 } format)
            return;

        switch (format.ToLowerInvariant())
        {
            case "none":
                break;
            case "beautify":
                ResponseBodyFormat = ResponseBodyFormat.Beautify;
                break;
            case "minify":
                ResponseBodyFormat = ResponseBodyFormat.Minify;
                break;
            default:
                Problem = $"settings.toml: response.format is \"{format}\", not none, beautify or minify";
                break;
        }
    }

    public async Task<string> EnsureFileAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(StraumrDir))
            Directory.CreateDirectory(StraumrDir);

        if (!File.Exists(SettingsPath))
            await File.WriteAllTextAsync(SettingsPath, StraumrSettings.Template, cancellationToken);

        return SettingsPath;
    }

    public async Task SetThemeAsync(string reference, CancellationToken cancellationToken = default)
    {
        string path = await EnsureFileAsync(cancellationToken);
        string text = await File.ReadAllTextAsync(path, cancellationToken);

        DocumentSyntax document = SyntaxParser.Parse(text, path, false);
        if (document.HasErrors)
            throw new TomlException($"settings.toml: {FirstLine(document.Diagnostics.ToString() ?? "unparsable")}");

        var value = new StringValueSyntax(reference);
        if (document.KeyValues.FirstOrDefault(Names("theme")) is { } existing)
            existing.Value = value;
        else
            document.KeyValues.Add(new KeyValueSyntax("theme", value));

        await File.WriteAllTextAsync(path, document.ToString(), cancellationToken);
        await LoadAsync(cancellationToken);
    }

    private static Func<KeyValueSyntax, bool> Names(string key) =>
        candidate => string.Equals(candidate.Key?.ToString()?.Trim(), key, StringComparison.Ordinal);

    /// <summary>
    /// Tomlyn reports every diagnostic it collected. The footer is one row, so it gets the first.
    /// </summary>
    private static string FirstLine(string message)
    {
        int end = message.IndexOfAny(['\r', '\n']);
        return end < 0 ? message : message[..end];
    }
}
