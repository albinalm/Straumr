using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Helpers;

internal static class ReferenceViewHelpers
{
    public static string Placeholder(string name, bool isSecret) =>
        isSecret ? $"{{{{secret:{name}}}}}" : $"{{{{{name}}}}}";

    public static Visual Create(string placeholder, string scope, IReadOnlyList<ReferenceUsageModel> usages,
        KnownReferenceService references)
    {
        List<Visual> content = new()
        {
            usages.Count == 0 ? Empty(placeholder, scope, references) : Workspaces(usages)
        };
        if (references.Notice is { } notice)
        {
            content.Add(new TextBlock(notice).Style(StraumrStyleService.AmberText).Wrap(true)
                .HorizontalAlignment(Align.Stretch));
        }

        content.Add(FieldListHelpers.Create(("Placeholder", FieldListHelpers.Wrapped(placeholder))));
        return new VStack(content.ToArray()).Spacing(1).HorizontalAlignment(Align.Stretch);
    }

    public static Visual Message(string text) =>
        new TextBlock(text).Style(StraumrStyleService.MutedText).Wrap(true).HorizontalAlignment(Align.Stretch);

    private static Visual Empty(string placeholder, string scope, KnownReferenceService references) =>
        Message(references.ScannedWorkspaces == 0
            ? $"No registered workspace could be read, so references to {placeholder} are unknown."
            : $"No request or auth in {scope} references {placeholder}.");

    private static Visual Workspaces(IReadOnlyList<ReferenceUsageModel> usages) =>
        new VStack(usages.GroupBy(usage => usage.WorkspaceId)
                .Select(group => (Visual)new VStack([
                        Header(group.First().Workspace, group.Count()),
                        .. group.Select(Entry)
                    ])
                    .HorizontalAlignment(Align.Stretch))
                .ToArray())
            .Spacing(1).HorizontalAlignment(Align.Stretch);

    private static Visual Header(string workspace, int count) =>
        StraumrSurfaceHelpers.Bar(
            new TextBlock(workspace).Style(StraumrStyleService.AccentText)
                .Trimming(TextTrimming.EndEllipsis).HorizontalAlignment(Align.Stretch),
            new TextBlock(CountFormatting.Label(count, "reference")).Style(StraumrStyleService.MutedText));

    private static Visual Entry(ReferenceUsageModel usage) =>
        StraumrSurfaceHelpers.Bar(
            new TextBlock($"  {SecretFormatting.Display(usage.Resource)}").Style(StraumrStyleService.PrimaryText)
                .Trimming(TextTrimming.EndEllipsis).HorizontalAlignment(Align.Stretch),
            new TextBlock($"{usage.Kind} · {usage.Field}").Style(StraumrStyleService.MutedText)
                .Trimming(TextTrimming.EndEllipsis));
}
