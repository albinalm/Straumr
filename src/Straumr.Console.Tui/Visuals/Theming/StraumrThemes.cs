using Tomlyn;
using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Visuals.Theming;

/// <summary>
/// Resolves what the settings file says the theme is into a palette the shell can be painted with.
/// </summary>
public static class StraumrThemes
{
    /// <summary>What a reader gets before they have said anything: the terminal's own colours.</summary>
    public const string DefaultReference = BuiltInThemes.TerminalName;

    private static readonly Dictionary<string, string> Sources = new(StringComparer.OrdinalIgnoreCase)
    {
        [BuiltInThemes.TerminalName] = BuiltInThemes.Terminal,
        [BuiltInThemes.StraumrName] = BuiltInThemes.Straumr
    };

    public static IEnumerable<string> BuiltInNames => Sources.Keys;

    /// <summary>The TOML a built-in is defined by, for writing it out as a file to start from.</summary>
    public static string? BuiltInSource(string name) => Sources.GetValueOrDefault(name.Trim());

    /// <summary>
    /// Resolves a built-in name or a theme file path. A relative name is first looked up under the
    /// <c>themes</c> directory beside the settings file, with <c>.toml</c> inferred when omitted.
    /// Explicit absolute paths and paths relative to the settings directory remain valid.
    /// </summary>
    public static bool TryResolve(
        string? reference,
        string baseDirectory,
        out StraumrTheme theme,
        out string? error)
    {
        theme = null!;
        error = null;
        string value = (reference ?? DefaultReference).Trim();
        if (value.Length == 0)
            value = DefaultReference;

        if (Sources.TryGetValue(value, out string? builtIn))
        {
            StraumrPalette? under = value.Equals(BuiltInThemes.TerminalName, StringComparison.OrdinalIgnoreCase)
                ? null
                : TerminalPalette;
            if (!TryParse(builtIn, under, out theme, out error))
                throw new InvalidOperationException($"the built-in theme '{value}' is malformed: {error}");
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
            return path;

        string fileName = Path.HasExtension(path) ? path : $"{path}.toml";
        string themePath = Path.Combine(baseDirectory, "themes", fileName);
        if (File.Exists(themePath))
            return themePath;

        // Keep paths written before the themes directory became the default working unchanged:
        // `themes/mine.toml` still resolves beside settings rather than becoming themes/themes/…,
        // and a custom file deliberately kept beside settings remains valid too.
        string legacyPath = Path.Combine(baseDirectory, path);
        return File.Exists(legacyPath) || Path.HasExtension(path)
            ? legacyPath
            : themePath;
    }

    /// <summary>
    /// The palette every other theme is measured against. Parsed from the terminal built-in, which
    /// is the one file that has to name every role because there is nothing under it.
    /// </summary>
    private static StraumrPalette TerminalPalette => field ??= ParseRoot();

    private static StraumrPalette ParseRoot()
    {
        if (!TryParse(BuiltInThemes.Terminal, null, out StraumrTheme theme, out string? error))
            throw new InvalidOperationException($"the terminal theme is malformed: {error}");

        return theme.Palette;
    }

    /// <param name="fallback">
    /// What a role the file does not mention takes. <see langword="null"/> only for the terminal
    /// theme itself, where every role is required instead.
    /// </param>
    private static bool TryParse(
        string text, StraumrPalette? fallback, out StraumrTheme theme, out string? error)
    {
        theme = null!;
        error = null;
        ThemeDocument document;
        try
        {
            document = TomlSerializer.Deserialize(text, ThemeTomlContext.Default.ThemeDocument)
                       ?? new ThemeDocument();
        }
        catch (TomlException exception)
        {
            error = FirstLine(exception.Message);
            return false;
        }

        // Spelling is normalised so a file may use dashes, underscores or the role's own casing.
        Dictionary<string, string> colors = new(StringComparer.Ordinal);
        foreach ((string key, string value) in document.Colors)
            colors[Normalize(key)] = value;

        // `selection = "invert"` is not a colour, so it is taken out before the colours are read.
        // The role keeps a colour all the same: a text selection inside a field is drawn by us and
        // has nothing to invert against, so it falls back to the raised surface. A file that does
        // not mention selection at all inherits whichever of the two the theme under it used.
        bool mentioned = colors.TryGetValue("selection", out string? band);
        bool inverts = mentioned
            ? band!.Trim().Equals("invert", StringComparison.OrdinalIgnoreCase)
            : fallback?.SelectionInverts ?? false;
        if (mentioned && inverts)
            colors["selection"] = colors.GetValueOrDefault("raised", "default");

        List<string> missing = [];
        Dictionary<string, Color> resolved = new(StringComparer.Ordinal);
        for (int index = 0; index < Roles.Length; index++)
        {
            string role = Roles[index];
            if (colors.Remove(role, out string? written))
            {
                if (!ThemeColor.TryParse(written, out Color color, out string? problem))
                {
                    error = $"{Display(role)} {problem}";
                    return false;
                }

                resolved[role] = color;
                continue;
            }

            if (fallback is null)
                missing.Add(role);
            else
                resolved[role] = Inherited[index](fallback);
        }

        if (missing.Count > 0)
        {
            error = $"missing {(missing.Count == 1 ? "colour" : "colours")}: {List(missing)}";
            return false;
        }

        if (!TryOptional(colors, "brand", fallback?.Brand ?? resolved["accent"], out Color brand, out error))
            return false;

        Dictionary<string, string> methods = new(StringComparer.Ordinal);
        foreach ((string key, string value) in document.Methods)
            methods[Normalize(key)] = value;

        MethodPalette inheritedMethods = fallback?.Methods ?? new MethodPalette(
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
                return false;
        }

        if (methods.Count > 0)
        {
            error = $"unknown {(methods.Count == 1 ? "method" : "methods")}: {List(methods.Keys)}";
            return false;
        }

        Dictionary<string, string> code = new(StringComparer.Ordinal);
        foreach ((string key, string value) in document.Code)
            code[Normalize(key)] = value;

        CodePalette inheritedCode = fallback?.Code ?? new CodePalette(
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
                return false;
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

        var palette = new StraumrPalette
        {
            Brand = brand,
            Methods = new MethodPalette(
                methodColors[0], methodColors[1], methodColors[2],
                methodColors[3], methodColors[4], methodColors[5]),
            Code = new CodePalette(
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

        theme = new StraumrTheme(
            string.IsNullOrWhiteSpace(document.Name) ? "unnamed" : document.Name.Trim(),
            palette,
            Collisions(palette));
        return true;
    }

    /// <summary>Reads each role out of the theme a file is layered over, in <see cref="Roles"/> order.</summary>
    private static readonly Func<StraumrPalette, Color>[] Inherited =
    [
        p => p.Background, p => p.Raised, p => p.Selection, p => p.SelectionInactive,
        p => p.Hover, p => p.Border, p => p.ScrollTrack, p => p.ScrollThumb,
        p => p.Text, p => p.TextBright, p => p.Muted, p => p.MutedBright,
        p => p.Accent, p => p.Amber, p => p.Green, p => p.Red, p => p.RedBright, p => p.Purple
    ];

    /// <summary>
    /// The pairs the shell paints one on top of the other. Equal colours there do not fail the
    /// theme, they only make a cue invisible — and an invisible cue is far harder to diagnose from
    /// the running app than a line in the footer is. A role painted on the terminal's own ground is
    /// exempt: there is no band there to be lost against.
    /// </summary>
    private static IReadOnlyList<string> Collisions(StraumrPalette palette)
    {
        List<string> warnings = [];

        // The trap a theme that writes only its differences can fall into: fixing the ground while
        // leaving the text inherited. `text` defers to the terminal, which may be dark or light, so
        // a named background it was never measured against can swallow it whole. Naming one without
        // the other is worth a word before the reader concludes the app is broken.
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
            // A background of `default` paints no band at all — the cell keeps the terminal's own
            // ground — so nothing can be lost against it and a foreground matching it is not a
            // collision. Themes that cannot name a tint of an unknown ground rely on this to drop a
            // band rather than guess at one.
            if (background.Kind == ColorKind.Default)
                return;

            if (foreground == background)
                warnings.Add($"{first} and {second} are the same colour, so {what} is invisible");
        }
    }

    /// <summary>
    /// Reads a role a theme need not write, removing it from <paramref name="written"/> so whatever
    /// is left over can still be reported as a typo.
    /// </summary>
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

        if (ThemeColor.TryParse(value, out color, out string? problem))
            return true;

        error = $"{Display(role)} {problem}";
        return false;
    }

    private static readonly string[] MethodRoles =
        ["get", "post", "put", "patch", "delete", "other"];

    private static readonly string[] CodeRoles =
        ["key", "string", "number", "boolean", "null", "punctuation"];

    /// <summary>
    /// Every colour role. Required of the terminal theme and optional of every other, which layers
    /// over it. The order is <see cref="Inherited"/>'s and the two must be kept in step.
    /// </summary>
    private static readonly string[] Roles =
    [
        "background", "raised", "selection", "selectioninactive", "hover", "border",
        "scrolltrack", "scrollthumb", "text", "textbright", "muted", "mutedbright",
        "accent", "amber", "green", "red", "redbright", "purple"
    ];

    /// <summary>
    /// Names the roles, stopping short of the whole list. A theme file that names none of them has
    /// all eighteen wrong, and the footer is one row: the first few and a count say the same thing
    /// in the space there is.
    /// </summary>
    private static string List(IEnumerable<string> roles)
    {
        string[] named = roles.Select(Display).ToArray();
        return named.Length <= 4
            ? string.Join(", ", named)
            : $"{string.Join(", ", named.Take(3))} and {named.Length - 3} more";
    }

    private static string Normalize(string key) =>
        new(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>Names a role back to the reader the way a theme file writes it.</summary>
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
