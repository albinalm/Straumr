using Straumr.Console.Tui.Infrastructure;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// The Secrets region's content: every secret reference a resource depends on, one to a row, with
/// whether the store can supply it. One that cannot reads red, since it is the reason an operation
/// will fail before it does.
/// </summary>
internal static class SecretList
{
    public static Visual Create(IReadOnlyList<SecretReference> references)
    {
        if (references.Count == 0)
            return new TextBlock("No secret references.").Style(StraumrStyles.MutedText).Wrap(true);

        return new VStack(references
                .Select(reference => (Visual)new HStack(
                        new TextBlock(reference.Name)
                            .Style(StraumrStyles.PurpleText)
                            .Trimming(TextTrimming.EndEllipsis),
                        new TextBlock(reference.Available ? "· available" : "· unavailable")
                            .Style(reference.Available ? StraumrStyles.MutedText : StraumrStyles.RedText))
                    .Spacing(1))
                .ToArray())
            .HorizontalAlignment(Align.Stretch);
    }
}
