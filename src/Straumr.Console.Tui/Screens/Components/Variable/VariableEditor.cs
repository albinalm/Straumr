using Straumr.Console.Tui.Screens.Components.Editor;
using Straumr.Core.Helpers;
using Straumr.Core.Enums;
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
    private VariableSource? _openedSource;

    public VariableEditor(StraumrVariable state, string? workspaceName, Guid? workspaceId, bool isNew,
        State<KnownReferenceService> references, IReadOnlyList<StraumrRequest> requests, Action save, Action closed, string? sourceName = null,
        bool focusValue = false, char? openingEcho = null)
    {
        _state = state;
        _openedName = new State<string>(state.Name);
        _openedValue = state.Value;
        _openedSource = state.Source;
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

        var valueField = new TextField("Value", state.Value, text => state.Value = text)
            { PendingEcho = focusValue ? openingEcho : null, Visible = () => state.Source is null };
        Guid? requestId = state.Source?.RequestId;
        ExtractionSource extractionSource = state.Source?.ExtractionSource ?? ExtractionSource.JsonPath;
        string expression = state.Source?.Expression ?? string.Empty;
        int? ttl = state.Source?.TimeToLiveSeconds;
        List<string> requestLabels = ["None (plain value)", .. requests.Select(request => request.Name)];
        List<Guid?> requestIds = [null, .. requests.Select(request => (Guid?)request.Id)];
        if (requestId is { } missing && !requestIds.Contains(missing))
        {
            requestLabels.Add($"{missing} · not in this workspace");
            requestIds.Add(missing);
        }
        void UpdateSource() => state.Source = requestId is { } id ? new VariableSource
        {
            RequestId = id,
            ExtractionSource = extractionSource,
            Expression = expression,
            TimeToLiveSeconds = ttl
        } : null;
        ChoiceField<Guid?> sourceRequest = new("Source request", requestLabels, requestIds, requestId,
            value =>
            {
                requestId = value;
                UpdateSource();
            });
        ExtractionSource[] extractionSources = Enum.GetValues<ExtractionSource>();
        ChoiceField<ExtractionSource> extraction = new("Extract from", extractionSources.Select(value => value.ToString()).ToList(),
            extractionSources, extractionSource, value =>
            {
                extractionSource = value;
                UpdateSource();
            })
            { Visible = () => state.Source is not null };
        var expressionField = new TextField("Expression", expression, value =>
            {
                expression = value;
                UpdateSource();
            },
            "session.sessionId", validate: value => state.Source is not null && string.IsNullOrWhiteSpace(value) ? "An expression is required." : null)
            { Visible = () => state.Source is not null };
        var ttlField = new TextField("TTL (seconds)", ttl?.ToString() ?? string.Empty,
            value =>
            {
                ttl = int.TryParse(value, out int seconds) ? seconds : null;
                UpdateSource();
            }, "until refreshed or inputs change",
            validate: value => value.Length == 0 || int.TryParse(value, out int seconds) && seconds > 0 ? null : "Enter a positive number of seconds.")
            { Visible = () => state.Source is not null };
        _view = new ResourceEditorView(
            () => state.Name.Length == 0 ? "new variable" : state.Name,
            () => workspaceName,
            new TextBlock("Workspace variable").Style(StraumrStyleService.MutedText),
            isNew,
            [
                new EditorForm("Variable", name,
                    sourceRequest, valueField, extraction, expressionField, ttlField,
                    new VariableReferenceField("References", _openedName, references, () => workspaceId))
            ],
            save, closed,
            () => state.Name != _openedName.Value || state.Value != _openedValue || state.Source != _openedSource,
            sourceName, focusValue ? state.Source is null ? valueField.FocusTarget : sourceRequest.FocusTarget : null);
    }

    public void Show() => _view.Show();
    public void Update() => _view.Update();
    public void Failed(string message) => _view.Failed(message);
    public void Report(string message, bool error) => _view.Report(message, error);

    public bool Saved()
    {
        _openedName.Value = _state.Name;
        _openedValue = _state.Value;
        _openedSource = _state.Source;
        return _view.Saved();
    }
}
