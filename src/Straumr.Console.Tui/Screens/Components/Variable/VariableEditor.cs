using Straumr.Console.Tui.Screens.Components.Editor;
using Straumr.Core.Helpers;
using Straumr.Core.Models;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Variable;

internal sealed class VariableEditor
{
    private readonly State<string> _openedName;
    private readonly StraumrVariable _state;
    private readonly ResourceEditorView _view;
    private string _openedValue;

    public VariableEditor(StraumrVariable state, string? workspaceName, Guid? workspaceId, bool isNew,
        State<KnownReferenceService> references, Action save, Action closed, string? sourceName = null)
    {
        _state = state;
        _openedName = new State<string>(state.Name);
        _openedValue = state.Value;
        var name = new TextField("Name", state.Name, value => state.Name = value,
                "variable name", validate: value => value.Length switch
                {
                    0 => "A name is required.",
                    _ when value.Contains('"') => "A name cannot contain a double quote.",
                    _ when value.Contains('{') || value.Contains('}') => "A name cannot contain braces.",
                    _ when VariableHelpers.IsSecretName(value) =>
                        $"A name cannot start with {VariableHelpers.SecretPrefix}; that prefix resolves a secret.",
                    _ => null
                })
            { PendingEcho = TuiKeybindHelpers.OpeningEcho };

        _view = new ResourceEditorView(
            () => state.Name.Length == 0 ? "new variable" : state.Name,
            () => workspaceName,
            new TextBlock("Workspace variable").Style(StraumrStyleService.MutedText),
            isNew,
            [
                new EditorForm("Variable", name,
                    new TextField("Value", state.Value, value => state.Value = value),
                    new VariableReferenceField("References", _openedName, references, () => workspaceId))
            ],
            save, closed,
            () => state.Name != _openedName.Value || state.Value != _openedValue,
            sourceName);
    }

    public void Show() => _view.Show();
    public void Update() => _view.Update();
    public void Failed(string message) => _view.Failed(message);
    public void Report(string message, bool error) => _view.Report(message, error);

    public void Saved()
    {
        _openedName.Value = _state.Name;
        _openedValue = _state.Value;
        _view.Saved();
    }
}
