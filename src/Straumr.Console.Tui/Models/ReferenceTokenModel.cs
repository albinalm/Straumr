namespace Straumr.Console.Tui.Models;

internal readonly record struct ReferenceTokenModel(int NameStart, int PrefixLength, int ReplaceLength, bool HasCloser,
    bool IsSecret);
