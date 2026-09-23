using Straumr.Core.Enums;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Formatting;

internal static class SyntaxHighlighting
{
    public static Func<string, StyledRun[]>? For(ContentLanguage language) => language switch
    {
        ContentLanguage.Json => JsonHighlighting.Line,
        ContentLanguage.Xml or ContentLanguage.Html => XmlHighlighting.Line,
        ContentLanguage.Yaml => YamlHighlighting.Line,
        ContentLanguage.FormUrlEncoded => FormHighlighting.Line,
        _ => null
    };
}
