using Tomlyn;
using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Services;

public static class StraumrThemeService
{
    public const string DefaultReference = BuiltInThemeHelpers.TerminalName;

    private static readonly Dictionary<string, string> Sources = new(StringComparer.OrdinalIgnoreCase)
    {
        [BuiltInThemeHelpers.TerminalName] = BuiltInThemeHelpers.Terminal,
        [BuiltInThemeHelpers.StraumrName] = BuiltInThemeHelpers.Straumr
    };

    private static readonly Func<StraumrPaletteModel, Color>[] Inherited =
    [
        p => p.Background, p => p.Raised, p => p.Selection, p => p.SelectionInactive,
        p => p.Hover, p => p.Border, p => p.ScrollTrack, p => p.ScrollThumb,
        p => p.Text, p => p.TextBright, p => p.Muted, p => p.MutedBright,
        p => p.Accent, p => p.Amber, p => p.Green, p => p.Red, p => p.RedBright, p => p.Purple
    ];

    private static readonly string[] MethodRoles =
        ["get", "post", "put", "patch", "delete", "other"];

    private static readonly string[] CodeRoles =
        ["key", "string", "number", "boolean", "null", "punctuation"];

    private static readonly string[] Roles =
    [
        "background", "raised", "selection", "selectioninactive", "hover", "border",
        "scrolltrack", "scrollthumb", "text", "textbright", "muted", "mutedbright",
        "accent", "amber", "green", "red", "redbright", "purple"
    ];

    public static IEnumerable<string> BuiltInNames => Sources.Keys;

    private static StraumrPaletteModel TerminalPalette => field ??= ParseRoot();

    public static string? BuiltInSource(string name) => Sources.GetValueOrDefault(name.Trim());

    public static IReadOnlyList<(string Name, string Path)> Installed(string settingsDirectory)
    {
        string folder = Path.Combine(settingsDirectory, "themes");
        if (!Directory.Exists(folder))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateFiles(folder, "*.toml")
                .Select(path => (Name: Path.GetFileNameWithoutExtension(path), Path: path))
                .Where(theme => theme.Name.Length > 0)
                .OrderBy(theme => theme.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static bool TryResolve(
        string? reference,
        string baseDirectory,
        out StraumrThemeModel theme,
        out string? error)
    {
        theme = null!;
        error = null;
        string value = (reference ?? DefaultReference).Trim();
        if (value.Length == 0)
        {
            value = DefaultReference;
        }

        if (Sources.TryGetValue(value, out string? builtIn))
        {
            StraumrPaletteModel? under = value.Equals(BuiltInThemeHelpers.TerminalName, StringComparison.OrdinalIgnoreCase)
                ? null
                : TerminalPalette;
            if (!TryParse(builtIn, under, out theme, out error))
            {
                throw new InvalidOperationException($"the built-in theme '{value}' is malformed: {error}");
            }

            return true;
        }

        string path = Resolve(value, baseDirectory);
        if (!File.Exists(path))
        {
            error = $"theme '{value}' is not a built-in ({string.Join(", ", Sources.Keys)}) " +
                    "and no file of that name was found";
            return false;
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"theme '{value}' could not be read: {exception.Message}";
            return false;
        }

        if (!TryParse(text, TerminalPalette, out theme, out string? problem))
        {
            error = $"theme '{value}': {problem}";
            return false;
        }

        return true;
    }

    private static string Resolve(string value, string baseDirectory)
    {
        string path = Environment.ExpandEnvironmentVariables(value);
        if (path.Length > 0 && path[0] == '~')
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path[1..].TrimStart('/', '\\'));
        }

        if (Path.IsPathRooted(path))
        {
            return path;
        }

        string fileName = Path.HasExtension(path) ? path : $"{path}.toml";
        string themePath = Path.Combine(baseDirectory, "themes", fileName);
        if (File.Exists(themePath))
        {
            return themePath;
        }

        string legacyPath = Path.Combine(baseDirectory, path);
        return File.Exists(legacyPath) || Path.HasExtension(path)
            ? legacyPath
            : themePath;
    }

    private static StraumrPaletteModel ParseRoot()
    {
        if (!TryParse(BuiltInThemeHelpers.Terminal, null, out StraumrThemeModel theme, out string? error))
        {
            throw new InvalidOperationException($"the terminal theme is malformed: {error}");
        }

        return theme.Palette;
    }

    private static bool TryParse(
        string text, StraumrPaletteModel? fallback, out StraumrThemeModel theme, out string? error)
    {
        theme = null!;
        error = null;
        ThemeDocumentModel document;
        try
        {
            document = TomlSerializer.Deserialize(text, ThemeTomlSerializerContext.Default.ThemeDocumentModel)
                       ?? new ThemeDocumentModel();
        }
        catch (TomlException exception)
        {
            error = FirstLine(exception.Message);
            return false;
        }

        Dictionary<string, string> colors = new(StringComparer.Ordinal);
        foreach ((string key, string value) in document.Colors)
        {
            colors[Normalize(key)] = value;
        }

        bool mentioned = colors.TryGetValue("selection", out string? band);
        bool inverts = mentioned
            ? band!.Trim().Equals("invert", StringComparison.OrdinalIgnoreCase)
            : fallback?.SelectionInverts ?? false;
        if (mentioned && inverts)
        {
            colors["selection"] = colors.GetValueOrDefault("raised", "default");
        }

        List<string> missing = [];
        Dictionary<string, Color> resolved = new(StringComparer.Ordinal);
        for (int index = 0; index < Roles.Length; index++)
        {
            string role = Roles[index];
            if (colors.Remove(role, out string? written))
            {
                if (!ThemeColorHelpers.TryParse(written, out Color color, out string? problem))
                {
                    error = $"{Display(role)} {problem}";
                    return false;
                }

                resolved[role] = color;
                continue;
            }

            if (fallback is null)
            {
                missing.Add(role);
            }
            else
            {
                resolved[role] = Inherited[index](fallback);
            }
        }

        if (missing.Count > 0)
        {
            error = $"missing {(missing.Count == 1 ? "colour" : "colours")}: {List(missing)}";
            return false;
        }

        if (!TryOptional(colors, "brand", fallback?.Brand ?? resolved["accent"], out Color brand, out error))
        {
            return false;
        }

        Dictionary<string, string> methods = new(StringComparer.Ordinal);
        foreach ((string key, string value) in document.Methods)
        {
            methods[Normalize(key)] = value;
        }

        MethodPaletteModel inheritedMethods = fallback?.Methods ?? new MethodPaletteModel(
            resolved["green"], resolved["accent"], resolved["amber"],
            resolved["purple"], resolved["red"], resolved["mutedbright"]);
        Color[] methodDefaults =
        [
            inheritedMethods.Get, inheritedMethods.Post, inheritedMethods.Put,
            inheritedMethods.Patch, inheritedMethods.Delete, inheritedMethods.Other
        ];

        Color[] methodColors = new Color[MethodRoles.Length];
        for (int index = 0; index < MethodRoles.Length; index++)
        {
            if (!TryOptional(methods, MethodRoles[index], methodDefaults[index], out methodColors[index], out error))
            {
                return false;
            }
        }

        if (methods.Count > 0)
        {
            error = $"unknown {(methods.Count == 1 ? "method" : "methods")}: {List(methods.Keys)}";
            return false;
        }

        Dictionary<string, string> code = new(StringComparer.Ordinal);
        foreach ((string key, string value) in document.Code)
        {
            code[Normalize(key)] = value;
        }

        CodePaletteModel inheritedCode = fallback?.Code ?? new CodePaletteModel(
            resolved["accent"], resolved["green"], resolved["amber"],
            resolved["purple"], resolved["muted"], resolved["muted"]);
        Color[] codeDefaults =
        [
            inheritedCode.Key, inheritedCode.String, inheritedCode.Number,
            inheritedCode.Boolean, inheritedCode.Null, inheritedCode.Punctuation
        ];

        Color[] codeColors = new Color[CodeRoles.Length];
        for (int index = 0; index < CodeRoles.Length; index++)
        {
            if (!TryOptional(code, CodeRoles[index], codeDefaults[index], out codeColors[index], out error))
            {
                return false;
            }
        }

        if (code.Count > 0)
        {
            error = $"unknown code {(code.Count == 1 ? "colour" : "colours")}: {List(code.Keys)}";
            return false;
        }

        if (colors.Count > 0)
        {
            error = $"unknown {(colors.Count == 1 ? "colour" : "colours")}: {List(colors.Keys)}";
            return false;
        }

        var palette = new StraumrPaletteModel
        {
            Brand = brand,
            Methods = new MethodPaletteModel(
                methodColors[0], methodColors[1], methodColors[2],
                methodColors[3], methodColors[4], methodColors[5]),
            Code = new CodePaletteModel(
                codeColors[0], codeColors[1], codeColors[2],
                codeColors[3], codeColors[4], codeColors[5]),
            Background = resolved["background"],
            Raised = resolved["raised"],
            Selection = resolved["selection"],
            SelectionInverts = inverts,
            SelectionInactive = resolved["selectioninactive"],
            Hover = resolved["hover"],
            Border = resolved["border"],
            ScrollTrack = resolved["scrolltrack"],
            ScrollThumb = resolved["scrollthumb"],
            Text = resolved["text"],
            TextBright = resolved["textbright"],
            Muted = resolved["muted"],
            MutedBright = resolved["mutedbright"],
            Accent = resolved["accent"],
            Amber = resolved["amber"],
            Green = resolved["green"],
            Red = resolved["red"],
            RedBright = resolved["redbright"],
            Purple = resolved["purple"]
        };

        theme = new StraumrThemeModel(
            string.IsNullOrWhiteSpace(document.Name) ? "unnamed" : document.Name.Trim(),
            palette,
            Collisions(palette));
        return true;
    }

    private static IReadOnlyList<string> Collisions(StraumrPaletteModel palette)
    {
        List<string> warnings = [];

        if (palette.Background.Kind != ColorKind.Default && palette.Text.Kind == ColorKind.Default)
        {
            warnings.Add("background names a colour but text is still the terminal's own, " +
                         "which may not read on it");
        }

        Check(palette.Muted, palette.Background, "muted", "background", "every second line");
        Check(palette.Muted, palette.Hover, "muted", "hover", "a hovered row's second line");
        if (!palette.SelectionInverts)
        {
            Check(palette.Accent, palette.Selection, "accent", "selection",
                "the marker on the selected row of a focused list");
            Check(palette.MutedBright, palette.Selection, "muted-bright", "selection",
                "a selected row's second line");
            Check(palette.TextBright, palette.Selection, "text-bright", "selection", "a selected row");
        }

        Check(palette.Muted, palette.SelectionInactive, "muted", "selection-inactive",
            "the marker on the selected row of an unfocused list");
        Check(palette.Text, palette.SelectionInactive, "text", "selection-inactive",
            "a selected row in an unfocused list");
        return warnings;

        void Check(Color foreground, Color background, string first, string second, string what)
        {
            if (background.Kind == ColorKind.Default)
            {
                return;
            }

            if (foreground == background)
            {
                warnings.Add($"{first} and {second} are the same colour, so {what} is invisible");
            }
        }
    }

    private static bool TryOptional(
        Dictionary<string, string> written,
        string role,
        Color fallback,
        out Color color,
        out string? error)
    {
        error = null;
        if (!written.Remove(role, out string? value))
        {
            color = fallback;
            return true;
        }

        if (ThemeColorHelpers.TryParse(value, out color, out string? problem))
        {
            return true;
        }

        error = $"{Display(role)} {problem}";
        return false;
    }

    private static string List(IEnumerable<string> roles)
    {
        string[] named = roles.Select(Display).ToArray();
        return named.Length <= 4
            ? string.Join(", ", named)
            : $"{string.Join(", ", named.Take(3))} and {named.Length - 3} more";
    }

    private static string Normalize(string key) =>
        new(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string Display(string role) => role switch
    {
        "selectioninactive" => "selection-inactive",
        "scrolltrack" => "scroll-track",
        "scrollthumb" => "scroll-thumb",
        "textbright" => "text-bright",
        "mutedbright" => "muted-bright",
        "redbright" => "red-bright",
        _ => role
    };

    private static string FirstLine(string message)
    {
        int end = message.IndexOfAny(['\r', '\n']);
        return end < 0 ? message : message[..end];
    }
}
