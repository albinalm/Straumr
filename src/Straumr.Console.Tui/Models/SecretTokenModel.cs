namespace Straumr.Console.Tui.Models;

internal readonly record struct SecretTokenModel(int NameStart, int PrefixLength, int ReplaceLength, bool HasCloser);
