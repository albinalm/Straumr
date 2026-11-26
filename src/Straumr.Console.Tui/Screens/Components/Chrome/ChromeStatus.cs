using System.Net;
using Straumr.Core.Models;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Screens.Components.Chrome;

internal static class ChromeStatus
{
    public const string Dot = "●";
    public const string Ring = "○";

    public static TextBlockStyle Style(StraumrResponse response) =>
        response.Exception is not null || response.StatusCode is null ? StraumrStyleService.RedText : Style((int)response.StatusCode.Value);

    public static TextBlockStyle Style(int code) => code switch
    {
        >= 500 => StraumrStyleService.RedText,
        >= 400 => StraumrStyleService.AmberText,
        >= 300 => StraumrStyleService.PurpleText,
        _ => StraumrStyleService.GreenText
    };

    public static string Code(StraumrResponse response) =>
        response.StatusCode is HttpStatusCode code ? ((int)code).ToString() : "ERR";

    public static ResourceTokenModel Token(StraumrResponse response) =>
        new($"{Dot} {Code(response)}", Style(response));

    public static TextBlock DotFor(Func<TextBlockStyle?> style) =>
        new TextBlock(() => style() is null ? Ring : Dot)
            .Style(() => style() ?? StraumrStyleService.MutedText);
}
