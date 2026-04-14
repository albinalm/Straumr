using Straumr.Console.Tui.Components.Prompts.Base;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts.Form;

public enum FormFieldPathMode
{
    None,
    Directory,
    ExistingFile
}

public sealed record FormFieldSpec(
    string Key,
    string Label,
    string? InitialValue = null,
    bool Required = false,
    Func<string, string?>? Validate = null,
    FormFieldPathMode PathMode = FormFieldPathMode.None,
    IReadOnlyList<IAllowedType>? PathAllowedTypes = null);

internal sealed class FormPrompt : PromptComponent
{
    public required string Title { get; init; }
    public required IReadOnlyList<FormFieldSpec> Fields { get; init; }
    public Func<FormFieldSpec, string?, string?>? BrowseForPath { get; init; }

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
            Theme = Theme,
            BrowseHandler = BrowseForPath,
        };

        _fieldsView.Submitted += result => Submitted?.Invoke(result);
        _fieldsView.CancelRequested += () => CancelRequested?.Invoke();

        frame.Add(_fieldsView);

        return frame;
    }
}
