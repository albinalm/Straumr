namespace Straumr.Console.Tui.Models;

internal sealed record SecretRewriteModel(int References, int Resources, IReadOnlyList<string> Problems);
