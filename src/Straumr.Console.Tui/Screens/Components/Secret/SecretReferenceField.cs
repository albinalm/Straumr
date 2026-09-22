using Straumr.Console.Tui.Screens.Components.Editor;
using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Secret;

internal sealed class SecretReferenceField : EditorField
{
    private readonly State<string> _name;
    private readonly State<KnownSecretReferenceService> _references;
    private readonly ScrollableContent _view;

    public SecretReferenceField(string label, State<string> name, State<KnownSecretReferenceService> references)
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
            ? SecretReferenceViewHelpers.Message("References are listed once the secret has been saved.")
            : SecretReferenceViewHelpers.Create(name, _references.Value);
    }
}
