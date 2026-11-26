using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class TextField : EditorField
{
    private const int DialogWidth = 70;

    private readonly FieldValue _display;
    private readonly State<string?> _placeholder;
    private readonly ValidationPresenter _presenter;
    private readonly bool _secret;
    private readonly Action<string> _set;
    private readonly State<string> _text;
    private readonly Func<string, string?>? _validate;

    public TextField(
        string label,
        string initial,
        Action<string> set,
        string? placeholder = null,
        bool secret = false,
        Func<string, string?>? validate = null)
        : base(label)
    {
        _set = set;
        _secret = secret;
        _validate = validate;
        _text = new State<string>(initial);
        _placeholder = new State<string?>(placeholder);
        _display = new FieldValue(Display, () => _text.Value.Length == 0, Edit);
        _presenter = EditorVisualHelpers.Validated(_display);
        Content = _presenter;
    }

    public override Visual Content { get; }

    public override Visual FocusTarget => _display;

    public string? Placeholder
    {
        get => _placeholder.Value;
        set => _placeholder.Value = value;
    }

    public string Value => _text.Value.Trim();

    public override string? Validate() => _validate?.Invoke(Value);

    public override void ShowProblem(string message) =>
        _presenter.Message = new ValidationMessage(ValidationSeverity.Error, new TextBlock(message));

    public override void ClearProblem() => _presenter.Message = null;

    public void Set(string value) => Apply(value);

    private string Display() =>
        _text.Value.Length == 0 ? _placeholder.Value ?? "empty"
        : _secret ? new string('•', Math.Min(_text.Value.Length, 12))
        : _text.Value;

    private void Edit(char? echo)
    {
        var dialog = new TextPromptDialog($"Edit {Label}", Label, _text.Value, "Save",
            value => _validate?.Invoke(value.Trim()), Apply, _placeholder.Value, _secret, width: DialogWidth, trim: false)
        {
            PendingEcho = echo
        };
        dialog.Show();
    }

    private void Apply(string value)
    {
        if (_text.Value == value)
        {
            return;
        }

        _text.Value = value;
        _presenter.Message = null;
        _set(value);
        Changed?.Invoke();
    }
}
