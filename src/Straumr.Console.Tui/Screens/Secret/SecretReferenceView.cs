using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Console.Tui.Visuals.Shared.Editor;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Secret;

internal static class SecretReferenceView
{
    public static string Placeholder(string name) => $"{{{{secret:{name}}}}}";

    public static Visual Create(string name, KnownSecretReferences references)
    {
        IReadOnlyList<SecretUsage> usages = references.For(name);
        var content = new List<Visual>
        {
            usages.Count == 0 ? Empty(name, references) : Workspaces(usages)
        };
        if (references.Notice is { } notice)
            content.Add(new TextBlock(notice).Style(StraumrStyles.AmberText).Wrap(true)
                .HorizontalAlignment(Align.Stretch));
        content.Add(FieldList.Create(("Placeholder", FieldList.Wrapped(Placeholder(name)))));
        return new VStack(content.ToArray()).Spacing(1).HorizontalAlignment(Align.Stretch);
    }

    public static Visual Message(string text) =>
        new TextBlock(text).Style(StraumrStyles.MutedText).Wrap(true).HorizontalAlignment(Align.Stretch);

    private static Visual Empty(string name, KnownSecretReferences references) =>
        Message(references.ScannedWorkspaces == 0
            ? $"No registered workspace could be read, so references to {Placeholder(name)} are unknown."
            : $"No request or auth in {CountFormatting.Label(references.ScannedWorkspaces, "scanned workspace")} " +
              $"references {Placeholder(name)}.");

    private static Visual Workspaces(IReadOnlyList<SecretUsage> usages) =>
        new VStack(usages.GroupBy(usage => usage.WorkspaceId)
                .Select(group => (Visual)new VStack([Header(group.First().Workspace, group.Count()),
                        .. group.Select(Entry)])
                    .HorizontalAlignment(Align.Stretch))
                .ToArray())
            .Spacing(1).HorizontalAlignment(Align.Stretch);

    private static Visual Header(string workspace, int count) =>
        StraumrSurfaces.Bar(
            new TextBlock(workspace).Style(StraumrStyles.AccentText)
                .Trimming(TextTrimming.EndEllipsis).HorizontalAlignment(Align.Stretch),
            new TextBlock(CountFormatting.Label(count, "reference")).Style(StraumrStyles.MutedText));

    private static Visual Entry(SecretUsage usage) =>
        StraumrSurfaces.Bar(
            new TextBlock($"  {SecretFormatting.Display(usage.Resource)}").Style(StraumrStyles.PrimaryText)
                .Trimming(TextTrimming.EndEllipsis).HorizontalAlignment(Align.Stretch),
            new TextBlock($"{usage.Kind} · {usage.Field}").Style(StraumrStyles.MutedText)
                .Trimming(TextTrimming.EndEllipsis));
}

internal sealed class SecretReferenceField : EditorField
{
    private readonly State<string> _name;
    private readonly State<KnownSecretReferences> _references;
    private readonly ScrollableContent _view;

    public SecretReferenceField(string label, State<string> name, State<KnownSecretReferences> references)
        : base(label)
    {
        (_name, _references) = (name, references);
        GrowsToFill = true;
        _view = new ScrollableContent(new ComputedVisual(Build));
        Content = _view;
    }

    public override Visual Content { get; }

    public override Visual FocusTarget => _view;

    private Visual Build()
    {
        string name = _name.Value.Trim();
        return name.Length == 0
            ? SecretReferenceView.Message("References are listed once the secret has been saved.")
            : SecretReferenceView.Create(name, _references.Value);
    }
}
