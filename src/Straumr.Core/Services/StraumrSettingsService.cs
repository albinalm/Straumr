using System.Text;
using Straumr.Core.Configuration;
using Straumr.Core.Services.Interfaces;
using Tomlyn;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace Straumr.Core.Services;

public class StraumrSettingsService : IStraumrSettingsService
{

    private const int DefaultHighlightLimit = 256 * 1024;

    private const int DefaultStoreLimit = 1024 * 1024;
    public StraumrSettingsService(string? settingsDirectory = null)
    {
        SettingsDirectory = settingsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".straumr");
    }

    public string SettingsPath => Path.Combine(SettingsDirectory, "settings.toml");

    public string SettingsDirectory { get; }

    public string? DefaultWorkspacePath => StraumrPathHelpers.ExpandOrNull(Settings.Paths.Workspaces);

    public string DefaultSecretPath =>
        StraumrPathHelpers.ExpandOrNull(Settings.Paths.Secrets) ?? Path.Combine(SettingsDirectory, "secrets");

    public StraumrSettings Settings { get; private set; } = new();

    public ResponseBodyFormat ResponseBodyFormat { get; private set; }

    public bool ResponseHighlight { get; private set; } = true;

    public int ResponseHighlightLimit { get; private set; } = DefaultHighlightLimit;

    public int ResponseStoreLimit { get; private set; } = DefaultStoreLimit;

    public string? Problem { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Problem = null;
        ResponseBodyFormat = ResponseBodyFormat.None;
        ResponseHighlight = true;
        ResponseHighlightLimit = DefaultHighlightLimit;
        ResponseStoreLimit = DefaultStoreLimit;
        string path = await EnsureFileAsync(cancellationToken);
        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Settings = new StraumrSettings();
            StraumrKeybinds.Apply(Settings.Keybinds);
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
            Settings = new StraumrSettings();
            StraumrKeybinds.Apply(Settings.Keybinds);
            Problem = $"settings.toml: {FirstLine(exception.Message)}";
            return;
        }

        ReadResponse();
        string? keybindProblem = StraumrKeybinds.Apply(Settings.Keybinds, Settings.KeybindPreset);
        Problem ??= keybindProblem;
    }

    public async Task<string> EnsureFileAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(SettingsDirectory))
        {
            Directory.CreateDirectory(SettingsDirectory);
        }

        if (!File.Exists(SettingsPath))
        {
            await File.WriteAllTextAsync(SettingsPath, StraumrSettings.Template, cancellationToken);
        }

        return SettingsPath;
    }

    public Task SetThemeAsync(string reference, CancellationToken cancellationToken = default)
        => SaveValuesAsync(new Dictionary<string, ValueSyntax>
        {
            ["theme"] = new StringValueSyntax(reference)
        }, null, cancellationToken);

    public Task CompleteQuickStartAsync(string preset, string theme, CancellationToken cancellationToken = default) =>
        SaveValuesAsync(new Dictionary<string, ValueSyntax>
        {
            ["theme"] = new StringValueSyntax(theme),
            ["quick-start-completed"] = new BooleanValueSyntax(true)
        }, StraumrKeybindPresets.Resolve(preset), cancellationToken);

    private void ReadResponse()
    {
        if (Settings.Response.Format?.Trim() is { Length: > 0 } format)
        {
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
                    Problem ??= $"settings.toml: response.format is \"{format}\", not none, beautify or minify";
                    break;
            }
        }

        ResponseHighlight = Settings.Response.Highlight ?? true;
        ResponseHighlightLimit = Bytes(
            Settings.Response.HighlightLimit, "highlight-limit", ResponseHighlightLimit);
        ResponseStoreLimit = Bytes(Settings.Response.StoreLimit, "store-limit", ResponseStoreLimit);
    }

    private int Bytes(int? kibibytes, string key, int fallback)
    {
        if (kibibytes is not { } size)
        {
            return fallback;
        }

        if (size < 0)
        {
            Problem ??= $"settings.toml: response.{key} is {size}, which is not a size in KiB";
            return fallback;
        }

        return size > int.MaxValue / 1024 ? int.MaxValue : size * 1024;
    }

    private async Task SaveValuesAsync(IReadOnlyDictionary<string, ValueSyntax> values,
        IReadOnlyDictionary<string, string>? keybinds, CancellationToken cancellationToken)
    {
        string path = await EnsureFileAsync(cancellationToken);
        string text;
        Encoding encoding;
        using (var reader = new StreamReader(path, new UTF8Encoding(false), true))
        {
            text = await reader.ReadToEndAsync(cancellationToken);
            encoding = reader.CurrentEncoding;
        }

        DocumentSyntax document = SyntaxParser.Parse(text, path, false);
        if (document.HasErrors)
        {
            throw new TomlException($"settings.toml: {FirstLine(document.Diagnostics.ToString() ?? "unparsable")}");
        }

        List<(int Start, int Length, string Value)> edits = new();
        List<string> additions = new();
        if (keybinds is not null && document.KeyValues.FirstOrDefault(Names("keybind-preset")) is { } stale)
        {
            edits.Add((stale.Span.Start.Offset, stale.Span.End.Offset - stale.Span.Start.Offset + 1, ""));
        }

        foreach ((string key, ValueSyntax value) in values)
        {
            if (document.KeyValues.FirstOrDefault(Names(key)) is { } existing)
            {
                // Replace only the value span so surrounding TOML comments and trivia survive.
                SourceSpan span = existing.Value!.Span;
                edits.Add((span.Start.Offset, span.End.Offset - span.Start.Offset + 1, value.ToString()));
            }
            else
            {
                additions.Add($"{key} = {value}");
            }
        }
        string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string appended = "";
        if (keybinds is not null)
        {
            Keybinds(document, keybinds, newline, edits, out appended);
        }

        foreach ((int Start, int Length, string Value) edit in edits.OrderByDescending(edit => edit.Start))
        {
            text = text.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Value);
        }
        if (additions.Count > 0)
        {
            // Root keys must precede TOML tables.
            text = string.Join(newline, additions) + newline + text;
        }

        if (appended.Length > 0)
        {
            text = text.TrimEnd('\r', '\n') + newline + newline + appended;
        }

        await File.WriteAllTextAsync(path, text, encoding, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    private static void Keybinds(DocumentSyntax document, IReadOnlyDictionary<string, string> keybinds,
        string newline, List<(int Start, int Length, string Value)> edits, out string appended)
    {
        appended = "";
        TableSyntaxBase? table = document.Tables.FirstOrDefault(candidate =>
            string.Equals(candidate.Name?.ToString()?.Trim(), "keybinds", StringComparison.Ordinal));
        List<string> lines = new();
        foreach ((string action, string value) in keybinds)
        {
            if (table?.Items.FirstOrDefault(Names(action)) is not { } existing)
            {
                lines.Add(Entry(action, value));
                continue;
            }

            SourceSpan span = existing.Value!.Span;
            edits.Add((span.Start.Offset, span.End.Offset - span.Start.Offset + 1,
                new StringValueSyntax(value).ToString()));
        }

        if (lines.Count == 0)
        {
            return;
        }

        if (table is null)
        {
            appended = "[keybinds]" + newline + string.Join(newline, lines) + newline;
            return;
        }

        int anchor = (table.Items.LastOrDefault() is { } last ? last.Span.End.Offset : table.Span.End.Offset) + 1;
        edits.Add((anchor, 0, string.Join("", lines.Select(line => line + newline))));
    }

    private static string Entry(string action, string value) =>
        $"{new StringValueSyntax(action)} = {new StringValueSyntax(value)}";

    private static Func<KeyValueSyntax, bool> Names(string key) =>
        candidate => candidate.Key is { } syntax && !syntax.DotKeys.Any() && string.Equals(syntax.Key switch
        {
            BareKeySyntax bare => bare.ToString().Trim(),
            StringValueSyntax quoted => quoted.Value,
            _ => null
        }, key, StringComparison.Ordinal);

    private static string FirstLine(string message)
    {
        int end = message.IndexOfAny(['\r', '\n']);
        return end < 0 ? message : message[..end];
    }
}
