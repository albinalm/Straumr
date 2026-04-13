namespace Straumr.Console.Tui.Helpers;

public static class HttpMethodMarkup
{
    public static string TagFor(string method)
        => $"method-{method.ToLowerInvariant()}";

    public static string TagFor(HttpMethod method)
        => TagFor(method.Method);
}
