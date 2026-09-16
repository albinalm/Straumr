using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Visuals.Shared.Editor;

/// <summary>
/// One labelled value in a resource editor. A field owns its control for the life of the form,
/// writes straight into the editor state it was given, and answers whether what it holds is
/// acceptable. It knows nothing about requests, auths or secrets — the kinds below are the complete
/// set the CLI's create and edit flows use across all three.
/// </summary>
internal abstract class EditorField
{
    protected EditorField(string label) => Label = label;

    public string Label { get; }

    /// <summary>
    /// Whether the field applies at all given the rest of the state. A discriminator — an auth type,
    /// a body type, an OAuth2 grant — hides the fields its other values own rather than showing them
    /// inert, because an inert field is indistinguishable from one that is merely empty.
    /// </summary>
    public Func<bool>? Visible { get; init; }

    public bool IsVisible => Visible?.Invoke() ?? true;

    /// <summary>
    /// Whether the field takes the height left over on its page rather than one row. A page has at
    /// most one: a key-value grid or a body, both of which are the reason that page exists.
    /// </summary>
    public bool GrowsToFill { get; protected init; }

    /// <summary>The whole field as it appears in the form, validation message included.</summary>
    public abstract Visual Content { get; }

    /// <summary>What receives focus when the form moves to this field.</summary>
    public abstract Visual FocusTarget { get; }

    /// <summary>Raised on every edit, so the form can mark itself unsaved.</summary>
    public Action? Changed { get; set; }

    /// <summary>
    /// Folds what the field holds back into the state being edited, for a value the field keeps in a
    /// shape of its own until it is asked — a document too large to marshal per keystroke, or a map
    /// that is stored as one encoded string.
    /// </summary>
    public Action? OnCommit { get; init; }

    public virtual void Commit() => OnCommit?.Invoke();

    /// <summary>The message to show, or <see langword="null"/> when the value is acceptable.</summary>
    public virtual string? Validate() => null;

    public virtual void ShowProblem(string message) { }

    public virtual void ClearProblem() { }
}

/// <summary>A single line of text, optionally masked and optionally validated.</summary>
internal sealed class TextField : EditorField
{
    private readonly FormTextBox _input;
    private readonly ValidationPresenter _presenter;
    private readonly Action<string> _set;
    private readonly Func<string, string?>? _validate;

    /// <param name="validate">
    /// Runs on submission rather than per keystroke, so a half-typed URL does not read as an error.
    /// </param>
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
        _presenter = EditorVisuals.Validated(_input);
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

    /// <summary>
    /// The hint shown while the field is empty, for a field whose hint another field decides: a
    /// custom auth's extraction expression means something different under each extraction source,
    /// and nothing else on that page says which.
    /// </summary>
    public string? Placeholder
    {
        get => _input.Placeholder;
        set => _input.Placeholder = value;
    }

    /// <summary>Arms the field to discard the keystroke that opened the form it leads.</summary>
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

    /// <summary>Writes the field from outside an edit, for a value another field decides.</summary>
    public void Set(string value)
    {
        if ((_input.Text ?? string.Empty) == value)
            return;
        _input.SetText(value);
        _set(value);
    }
}

/// <summary>
/// One value out of a fixed set, shown by a label of its own rather than by its identifier: a body
/// type reads as <c>Form URL Encoded</c> and not as <c>FormUrlEncoded</c>, and an auth reads by its
/// name and not by its id. The framework's <c>EnumSelect</c> cannot do either, so this drives a
/// plain <c>Select</c> over labels and keeps the values beside them.
/// </summary>
internal sealed class ChoiceField<T> : EditorField
{
    private readonly Select<string> _select;
    private readonly IReadOnlyList<T> _values;
    private readonly Action<T> _set;

    public ChoiceField(
        string label,
        IReadOnlyList<string> labels,
        IReadOnlyList<T> values,
        T initial,
        Action<T> set)
        : base(label)
    {
        _values = values;
        _set = set;
        int index = Math.Max(0, values.ToList().FindIndex(value => EqualityComparer<T>.Default.Equals(value, initial)));
        _select = new Select<string>(labels, index)
        {
            HorizontalAlignment = Align.Stretch
        };
        _select.SetStyle(StraumrStyles.Select);
        _select.WithMovementKeys();
        // A focusable inside a hidden subtree still takes a Tab; see FocusScope.
        _select.IsTabStop(_select.IsReachable);
        _select.SelectionChanged(() =>
        {
            _set(Value);
            Changed?.Invoke();
        });
        Content = _select;
    }

    public override Visual Content { get; }

    public override Visual FocusTarget => _select;

    public T Value => _values[Math.Clamp(_select.SelectedIndex, 0, _values.Count - 1)];
}

/// <summary>A yes/no value. The label sits in the form's label column, so the switch carries none.</summary>
internal sealed class ToggleField : EditorField
{
    private readonly Switch _switch;

    public ToggleField(string label, bool initial, Action<bool> set) : base(label)
    {
        _switch = new Switch { IsOn = initial };
        _switch.SetStyle(StraumrStyles.Switch);
        // A focusable inside a hidden subtree still takes a Tab; see FocusScope.
        _switch.IsTabStop(_switch.IsReachable);
        _switch.Toggled(() =>
        {
            set(_switch.IsOn);
            Changed?.Invoke();
        });
        Content = new HStack(_switch).HorizontalAlignment(Align.Start);
    }

    public override Visual Content { get; }

    public override Visual FocusTarget => _switch;
}

/// <summary>
/// Not a field but a statement in a field's place: what a page says when a discriminator has left it
/// with nothing to fill in. A page that simply went blank would read as one that had failed to load.
/// </summary>
internal sealed class MessageField : EditorField
{
    public MessageField(string label, string message) : base(label)
    {
        GrowsToFill = true;
        Content = ResourceScreenLayout.Message(
            new TextBlock(message)
                .Style(StraumrStyles.MutedText)
                .Wrap(true)
                .Trimming(TextTrimming.EndEllipsis));
    }

    public override Visual Content { get; }

    /// <summary>
    /// The page's own root. Nothing here takes focus, so the form's entry point falls through to
    /// whatever else is visible, and to the page itself when nothing is.
    /// </summary>
    public override Visual FocusTarget => Content;
}

/// <summary>Shared construction the field kinds would otherwise each repeat.</summary>
internal static class EditorVisuals
{
    public static ValidationPresenter Validated(Visual input)
    {
        var presenter = new ValidationPresenter(input)
        {
            Placement = ValidationPlacement.Below
        };
        presenter.SetStyle(StraumrStyles.Validation);
        return presenter;
    }
}
