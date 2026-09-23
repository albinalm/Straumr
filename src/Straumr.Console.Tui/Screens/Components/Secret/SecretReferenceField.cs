using Straumr.Console.Tui.Screens.Components.Editor;
using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Secret;

internal sealed class SecretReferenceField : EditorField
{
    private readonly State<string> _name;
    private readonly State<KnownReferenceService> _references;
    private readonly ScrollableContent _view;

    public SecretReferenceField(string label, State<string> name, State<KnownReferenceService> references)
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
            ? ReferenceViewHelpers.Message("References are listed once the secret has been saved.")
            : ReferenceViewHelpers.Create(ReferenceViewHelpers.Placeholder(name, true),
                CountFormatting.Label(_references.Value.ScannedWorkspaces, "scanned workspace"),
                _references.Value.ForSecret(name), _references.Value);
    }
}
