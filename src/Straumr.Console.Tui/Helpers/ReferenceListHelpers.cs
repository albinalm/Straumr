using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Helpers;

internal static class ReferenceListHelpers
{
    public static Visual Create(IReadOnlyList<ReferenceModel> references)
    {
        if (references.Count == 0)
        {
            return new TextBlock("No variable or secret references.").Style(StraumrStyleService.MutedText).Wrap(true);
        }

        return new VStack(references
                .OrderBy(reference => reference.IsSecret)
                .ThenBy(reference => reference.Name, StringComparer.OrdinalIgnoreCase)
                .Select(reference => (Visual)new HStack(
                        new TextBlock(reference.Name)
                            .Style(StraumrStyleService.PurpleText)
                            .Trimming(TextTrimming.EndEllipsis),
                        new TextBlock(reference.IsSecret ? "· secret" : "· variable")
                            .Style(StraumrStyleService.MutedText),
                        new TextBlock(reference.Available ? "· available" : "· unavailable")
                            .Style(reference.Available ? StraumrStyleService.MutedText : StraumrStyleService.RedText))
                    .Spacing(1))
                .ToArray())
            .HorizontalAlignment(Align.Stretch);
    }
}
