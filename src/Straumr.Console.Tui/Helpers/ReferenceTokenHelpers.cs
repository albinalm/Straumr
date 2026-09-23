using Straumr.Core.Helpers;

namespace Straumr.Console.Tui.Helpers;

internal static class ReferenceTokenHelpers
{
    private const string Opener = "{{";

    private const string Closer = "}}";

    public static ReferenceTokenModel? At(string text, int caret)
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

        bool isSecret = VariableHelpers.IsSecretName(text[start..end]);
        if (isSecret)
        {
            start += text.AsSpan(start, end - start).IndexOf(':') + 1;
        }

        ReadOnlySpan<char> tail = text.AsSpan(end);
        int brace = tail.IndexOfAny('{', '}');
        bool hasCloser = brace >= 0 && tail[brace..].StartsWith(Closer.AsSpan(), StringComparison.Ordinal);
        int rest = hasCloser ? brace : 0;
        return new ReferenceTokenModel(
            start,
            end - start,
            end - start + rest,
            hasCloser,
            isSecret);
    }

    public static string Prefix(string text, ReferenceTokenModel token) =>
        text.Substring(token.NameStart, token.PrefixLength);

    public static string Replacement(ReferenceTokenModel token, string name) =>
        token.HasCloser ? name : name + Closer;

    public static int CaretAfter(ReferenceTokenModel token, string name) =>
        token.NameStart + name.Length + Closer.Length;
}
