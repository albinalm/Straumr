using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Helpers;

internal static class SecretListHelpers
{
    public static Visual Create(IReadOnlyList<SecretReferenceModel> references)
    {
        if (references.Count == 0)
        {
            return new TextBlock("No secret references.").Style(StraumrStyleService.MutedText).Wrap(true);
        }

        return new VStack(references
                .Select(reference => (Visual)new HStack(
                        new TextBlock(reference.Name)
                            .Style(StraumrStyleService.PurpleText)
                            .Trimming(TextTrimming.EndEllipsis),
                        new TextBlock(reference.Available ? "· available" : "· unavailable")
                            .Style(reference.Available ? StraumrStyleService.MutedText : StraumrStyleService.RedText))
                    .Spacing(1))
                .ToArray())
            .HorizontalAlignment(Align.Stretch);
    }
}
