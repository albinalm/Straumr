namespace Straumr.Console.Tui.Models;

internal sealed record ReferenceRewriteModel(int References, int Resources, IReadOnlyList<string> Problems);
