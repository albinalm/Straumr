using Straumr.Console.Tui.Visuals.Shared;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Formatting;

/// <summary>
/// How a request's method reads. The mapping lives in the theme rather than here: a method colour
/// is the app's own vocabulary, and a reader whose scheme has no room for five distinct hues needs
/// to be able to say so.
/// </summary>
internal static class HttpMethodFormatting
{
    public static TextBlockStyle Style(HttpMethod method) =>
        StraumrStyles.MethodText(method.Method);
}
