using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Console.Tui.Visuals.Shared.Editor;
using Straumr.Core.Models;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Secret;

internal sealed class SecretEditor
{
    private readonly StraumrSecret _state;
    private readonly ResourceEditorView _view;
    private string _openedName;
    private string _openedValue;

    public SecretEditor(StraumrSecret state, string? workspaceName, bool isNew, char? openingGesture,
        Action save, Action closed, string? sourceName = null)
    {
        _state = state;
        _openedName = state.Name;
        _openedValue = state.Value;
        var name = new TextField("Name", state.Name, value => state.Name = value,
            placeholder: "secret name", validate: value => value.Length switch
            {
                0 => "A name is required.",
                _ when value.Contains('"') => "A name cannot contain a double quote.",
                _ => null
            }) { PendingEcho = openingGesture };

        _view = new ResourceEditorView(
            () => state.Name.Length == 0 ? "new secret" : SecretFormatting.Display(state.Name),
            () => workspaceName,
            new TextBlock("Global secret").Style(StraumrStyles.MutedText),
            isNew,
            [new EditorForm("Secret", name,
                new TextField("Value", state.Value, value => state.Value = value, secret: true),
                new MessageField("References", "References use the name. Renaming a secret does not update its references."))],
            save, closed,
            () => state.Name != _openedName || state.Value != _openedValue,
            sourceName);
    }

    public void Show() => _view.Show();
    public void Update() => _view.Update();
    public void Failed(string message) => _view.Failed(message);
    public void Report(string message, bool error) => _view.Report(message, error);

    public void Saved()
    {
        _openedName = _state.Name;
        _openedValue = _state.Value;
        _view.Saved();
    }
}
