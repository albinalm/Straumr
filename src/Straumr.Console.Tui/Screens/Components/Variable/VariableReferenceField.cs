using Straumr.Console.Tui.Screens.Components.Editor;
using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Variable;

internal sealed class VariableReferenceField : EditorField
{
    private readonly State<string> _name;
    private readonly State<KnownReferenceService> _references;
    private readonly ScrollableContent _view;
    private readonly Func<Guid?> _workspaceId;

    public VariableReferenceField(string label, State<string> name, State<KnownReferenceService> references,
        Func<Guid?> workspaceId)
        : base(label)
    {
        (_name, _references, _workspaceId) = (name, references, workspaceId);
        GrowsToFill = true;
        _view = new ScrollableContent(new ComputedVisual(Build));
        Content = _view;
    }

    public override Visual Content { get; }

    public override Visual FocusTarget => _view;

    private Visual Build()
    {
        string name = _name.Value.Trim();
        if (name.Length == 0 || _workspaceId() is not { } workspace)
        {
            return ReferenceViewHelpers.Message("References are listed once the variable has been saved.");
        }

        return ReferenceViewHelpers.Create(ReferenceViewHelpers.Placeholder(name, false), "this workspace",
            _references.Value.ForVariable(workspace, name), _references.Value);
    }
}
