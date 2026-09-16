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
        [BuiltInThemes.DeepOceanName] = BuiltInThemes.DeepOcean
    };

    public static IEnumerable<string> BuiltInNames => Sources.Keys;

    /// <summary>The TOML a built-in is defined by, for writing it out as a file to start from.</summary>
    public static string? BuiltInSource(string name) => Sources.GetValueOrDefault(name.Trim());

    /// <summary>
    /// Resolves a built-in name or a theme file path. A relative path is taken against
    /// <paramref name="baseDirectory"/> — the settings file's own directory — so a theme kept beside
    /// the settings that names it can be referred to by that name alone.
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
            if (!TryParse(builtIn, out theme, out error))
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

        if (!TryParse(text, out theme, out string? problem))
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

        return Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path);
    }

    private static bool TryParse(string text, out StraumrTheme theme, out string? error)
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
        // has nothing to invert against, so it falls back to the raised surface.
        bool inverts = colors.TryGetValue("selection", out string? band) &&
                       band.Trim().Equals("invert", StringComparison.OrdinalIgnoreCase);
        if (inverts)
            colors["selection"] = colors.GetValueOrDefault("raised", "default");

        List<string> missing = [];
        Dictionary<string, Color> resolved = new(StringComparer.Ordinal);
        foreach (string role in Roles)
        {
            if (!colors.Remove(role, out string? written))
            {
                missing.Add(role);
                continue;
            }

            if (!ThemeColor.TryParse(written, out Color color, out string? problem))
            {
                error = $"{Display(role)} {problem}";
                return false;
            }

            resolved[role] = color;
        }

        if (missing.Count > 0)
        {
            error = $"missing {(missing.Count == 1 ? "colour" : "colours")}: {List(missing)}";
            return false;
        }

        // Optional roles. They default to what the app used before they existed, so a theme file
        // that predates them is complete rather than broken — which is the only kind of extension a
        // format people keep in a dotfile repo can afford.
        if (!TryOptional(colors, "brand", resolved["accent"], out Color brand, out error))
            return false;

        Dictionary<string, string> methods = new(StringComparer.Ordinal);
        foreach ((string key, string value) in document.Methods)
            methods[Normalize(key)] = value;

        Color[] methodColors = new Color[MethodRoles.Length];
        Color[] methodDefaults =
        [
            resolved["green"], resolved["accent"], resolved["amber"],
            resolved["purple"], resolved["red"], resolved["mutedbright"]
        ];
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

    /// <summary>
    /// The pairs the shell paints one on top of the other. Equal colours there do not fail the
    /// theme, they only make a cue invisible — and an invisible cue is far harder to diagnose from
    /// the running app than a line in the footer is. A role painted on the terminal's own ground is
    /// exempt: there is no band there to be lost against.
    /// </summary>
    private static IReadOnlyList<string> Collisions(StraumrPalette palette)
    {
        List<string> warnings = [];
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
