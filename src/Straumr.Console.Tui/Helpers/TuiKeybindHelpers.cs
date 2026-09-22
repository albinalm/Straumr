using Straumr.Core.Configuration;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Helpers;

internal static class TuiKeybindHelpers
{

    public static char? OpeningEcho { get; private set; }
    public static KeyGesture? Get(string id)
    {
        bool letterVariant = id.EndsWith(".Letter", StringComparison.Ordinal) || id == "PagedPane.NextPageLetter";
        bool shiftedVariant = id.EndsWith(".Shifted", StringComparison.Ordinal);
        if (StraumrKeybinds.Get(Canonical(id)) is not { } binding)
        {
            return null;
        }

        TerminalModifiers modifiers = Modifiers(binding.Modifiers);
        if (binding.Character is not { } character)
        {
            return letterVariant || shiftedVariant ? null : new KeyGesture(Enum.Parse<TerminalKey>(binding.Key!), modifiers);
        }

        if (letterVariant)
        {
            return (modifiers & TerminalModifiers.Ctrl) != 0 && char.IsAsciiLetter(character)
                ? new KeyGesture(char.ToLowerInvariant(character), modifiers) : null;
        }

        if (shiftedVariant)
        {
            return (modifiers & (TerminalModifiers.Ctrl | TerminalModifiers.Alt)) == 0
                ? new KeyGesture(character, modifiers | TerminalModifiers.Shift) : null;
        }

        if ((modifiers & TerminalModifiers.Ctrl) != 0 && char.IsAsciiLetter(character))
        {
            character = (char)(char.ToUpperInvariant(character) & 0x1f);
        }

        return new KeyGesture(character, modifiers);
    }

    public static bool Matches(string id, KeyEventArgs e) =>
        StraumrKeybinds.Get(Canonical(id))?.Matches(e.Char ?? '\0', e.Key.ToString(), Modifiers(e.Modifiers)) == true;

    public static string Hint(string id) => StraumrKeybinds.Hint(Canonical(id));
    public static char? Echo(string id) => StraumrKeybinds.Echo(Canonical(id));

    public static string CombinedLabel(string label, params string[] actions) => string.Join(" ",
        actions.Where(id => Get(id) is not null).Select(id => StraumrStyleService.KeyMarkup(Hint(id))).Append(label));

    public static CommandPresentation SecondaryPresentation(string primary) =>
        Get(primary) is null ? CommandPresentation.CommandBar : CommandPresentation.None;

    public static void Run(string id, Action action)
    {
        char? previous = OpeningEcho;
        OpeningEcho = Echo(id);
        try { action(); }
        finally { OpeningEcho = previous; }
    }

    private static string Canonical(string id)
    {
        if (id == "ContentField.Edit.Letter")
        {
            return "ContentField.Edit.Control";
        }

        if (id.EndsWith(".Letter", StringComparison.Ordinal))
        {
            id = id[..^7];
        }

        if (id.EndsWith(".Shifted", StringComparison.Ordinal))
        {
            id = id[..^8];
        }

        return id switch
        {
            "PagedPane.NextPageLetter" => "PagedPane.NextPage",
            "ResourceScreen.ResizeLeftStacked" => "ResourceScreen.ResizeLeft",
            "Request.Send" => "Request.Send",
            _ when id.StartsWith("Secret.EditJson.", StringComparison.Ordinal) => "Secret.EditJson",
            _ => id
        };
    }

    private static TerminalModifiers Modifiers(ConsoleModifiers value) =>
        ((value & ConsoleModifiers.Control) != 0 ? TerminalModifiers.Ctrl : 0) |
        ((value & ConsoleModifiers.Alt) != 0 ? TerminalModifiers.Alt : 0) |
        ((value & ConsoleModifiers.Shift) != 0 ? TerminalModifiers.Shift : 0);

    private static ConsoleModifiers Modifiers(TerminalModifiers value) =>
        ((value & TerminalModifiers.Ctrl) != 0 ? ConsoleModifiers.Control : 0) |
        ((value & TerminalModifiers.Alt) != 0 ? ConsoleModifiers.Alt : 0) |
        ((value & TerminalModifiers.Shift) != 0 ? ConsoleModifiers.Shift : 0);
}
