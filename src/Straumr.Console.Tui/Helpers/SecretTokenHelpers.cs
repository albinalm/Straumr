namespace Straumr.Console.Tui.Helpers;

internal static class SecretTokenHelpers
{
    private const string Opener = "{{secret:";

    private const string Closer = "}}";

    public static SecretTokenModel? At(string text, int caret)
    {
        int end = Math.Clamp(caret, 0, text.Length);
        int opener = text.AsSpan(0, end).LastIndexOf(Opener.AsSpan());
        if (opener < 0)
        {
            return null;
        }

        int start = opener + Opener.Length;
        if (text.AsSpan(start, end - start).IndexOfAny('{', '}') >= 0)
        {
            return null;
        }

        ReadOnlySpan<char> tail = text.AsSpan(end);
        int brace = tail.IndexOfAny('{', '}');
        int rest = brace < 0 ? tail.Length : brace;
        return new SecretTokenModel(
            start,
            end - start,
            end - start + rest,
            tail[rest..].StartsWith(Closer.AsSpan(), StringComparison.Ordinal));
    }

    public static string Prefix(string text, SecretTokenModel token) =>
        text.Substring(token.NameStart, token.PrefixLength);

    public static string Replacement(SecretTokenModel token, string name) =>
        token.HasCloser ? name : name + Closer;

    public static int CaretAfter(SecretTokenModel token, string name) =>
        token.NameStart + name.Length + Closer.Length;
}
