namespace Straumr.Core.Constants;

internal static class StraumrHttpClientConstants
{
    public const string Default = "straumr-request";
    public const string Redirects = "straumr-request-redirects";
    public const string Insecure = "straumr-request-insecure";
    public const string InsecureRedirects = "straumr-request-insecure-redirects";

    public static string For(SendOptions? options)
    {
        return (options?.Insecure == true, options?.FollowRedirects == true) switch
        {
            (false, false) => Default,
            (false, true) => Redirects,
            (true, false) => Insecure,
            (true, true) => InsecureRedirects
        };
    }
}
