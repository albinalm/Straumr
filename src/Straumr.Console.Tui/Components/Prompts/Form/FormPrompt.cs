using Straumr.Console.Tui.Components.Prompts.Base;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts.Form;

public sealed record FormFieldSideAction(string Label, Func<string?, string?> Invoke);

public sealed record FormCustomCommand(char ShortcutKey, string HintText, Action<FormCommandContext> Invoke);

public sealed class FormCommandContext
{
    private readonly Func<Dictionary<string, string>> _getValues;
    private readonly Action<string, string?> _setValue;
    private readonly Action<string> _focusField;

    internal FormCommandContext(
        Func<Dictionary<string, string>> getValues,
        Action<string, string?> setValue,
        Action<string> focusField)
    {
        _getValues = getValues;
        _setValue = setValue;
        _focusField = focusField;
    }

    public IReadOnlyDictionary<string, string> Values => _getValues();

    public string? GetValue(string key)
        => Values.TryGetValue(key, out string? value) ? value : null;

    public void SetValue(string key, string? value) => _setValue(key, value);

    public void FocusField(string key) => _focusField(key);
}

public sealed record FormFieldSpec(
    string Key,
    string Label,
    string? InitialValue = null,
    bool Required = false,
    Func<string, string?>? Validate = null,
    FormFieldSideAction? SideAction = null);

internal sealed class FormPrompt : PromptComponent
{
    public required string Title { get; init; }
    public required IReadOnlyList<FormFieldSpec> Fields { get; init; }
    public IReadOnlyList<FormCustomCommand> CustomCommands { get; init; } = [];
    public bool AnyFieldEditing => _fieldsView?.AnyFieldEditing ?? false;

    public event Action<Dictionary<string, string>>? Submitted;
    public event Action? CancelRequested;

    private FormFieldsView? _fieldsView;

    public override View Build()
    {
        FrameView frame = CreateFrame(Title);
        _fieldsView = new FormFieldsView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Fields = Fields,
            CustomCommands = CustomCommands,
            Theme = Theme,
        };

        _fieldsView.Submitted += result => Submitted?.Invoke(result);
        _fieldsView.CancelRequested += () => CancelRequested?.Invoke();

        frame.Add(_fieldsView);

        return frame;
    }

    internal bool HandleFormKeyDown(Key key) => _fieldsView?.HandleFormKeyDown(key) ?? false;
}
