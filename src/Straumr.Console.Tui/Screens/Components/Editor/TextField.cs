using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class TextField : EditorField
{
    private readonly FormTextBox _input;
    private readonly ValidationPresenter _presenter;
    private readonly Action<string> _set;
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
        _validate = validate;
        _input = FormTextBox.Create(placeholder, secret);
        _input.SetText(initial);
        _presenter = EditorVisualHelpers.Validated(_input);
        _input.Changed = () =>
        {
            _presenter.Message = null;
            _set(_input.Text ?? string.Empty);
            Changed?.Invoke();
        };
        Content = _presenter;
    }

    public override Visual Content { get; }

    public override Visual FocusTarget => _input;

    public string? Placeholder
    {
        get => _input.Placeholder;
        set => _input.Placeholder = value;
    }

    public char? PendingEcho
    {
        get => _input.PendingEcho;
        set => _input.PendingEcho = value;
    }

    public string Value => (_input.Text ?? string.Empty).Trim();

    public override string? Validate() => _validate?.Invoke(Value);

    public override void ShowProblem(string message) =>
        _presenter.Message = new ValidationMessage(ValidationSeverity.Error, new TextBlock(message));

    public override void ClearProblem() => _presenter.Message = null;

    public void Set(string value)
    {
        if ((_input.Text ?? string.Empty) == value)
        {
            return;
        }

        _input.SetText(value);
        _set(value);
    }
}
