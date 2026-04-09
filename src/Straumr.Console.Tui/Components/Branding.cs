namespace Straumr.Console.Tui.Components;

internal static class Branding
{
    public const string Figlet = """
                                       _
                                   ___| |_ _ __ __ _ _   _ _ __ ___  _ __
                                  / __| __| '__/ _` | | | | '_ ` _ \| '__|
                                  \__ \ |_| | | (_| | |_| | | | | | | |
                                  |___/\__|_|  \__,_|\__,_|_| |_| |_|_|

                                  """;

    public static readonly int FigletWidth = Figlet.Split('\n').Max(l => l.Length);
    public static readonly int FigletHeight = Figlet.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
}
