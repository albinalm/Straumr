using Straumr.Console.Tui.Visuals.Shared;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Formatting;

internal static class HttpMethodFormatting
{
    public static TextBlockStyle Style(HttpMethod method) =>
        method.Method.ToUpperInvariant() switch
        {
            "GET" => StraumrStyles.GreenText,
            "POST" => StraumrStyles.AccentText,
            "PUT" => StraumrStyles.AmberText,
            "PATCH" => StraumrStyles.PurpleText,
            "DELETE" => StraumrStyles.RedText,
            _ => StraumrStyles.MutedBrightText
        };
}
