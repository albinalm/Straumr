using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Formatting;

internal static class HttpMethodFormatting
{
    public static TextBlockStyle Style(HttpMethod method) =>
        StraumrStyleService.MethodText(method.Method);
}
