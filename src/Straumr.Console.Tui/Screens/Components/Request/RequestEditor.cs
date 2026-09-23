using Straumr.Console.Tui.Screens.Components.Editor;
using Straumr.Core.Models;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Request;

internal sealed class RequestEditor
{
    private const string NoAuth = "None";

    private readonly KeyValueField _headers;

    private readonly RequestEditorStateModel _state;

    private readonly State<string> _summaryMethod;

    private readonly State<string> _summaryUri;

    private readonly ResourceEditorView _view;

    private RequestEditorStateModel _opened;

    public RequestEditor(
        RequestEditorStateModel state,
        IReadOnlyList<StraumrAuth> auths,
        string? workspaceName,
        bool isNew,
        Action save,
        Action closed,
        Action<ExternalContentEditModel> editContent,
        string? sourceName = null)
    {
        _state = state;
        _opened = state.Copy();
        _summaryMethod = new State<string>(state.Method);
        _summaryUri = new State<string>(state.Uri.Length == 0
            ? string.Empty
            : SecretFormatting.Display(state.GetDisplayUri()));

        var name = new TextField("Name", state.Name, value => state.Name = value,
            "request name",
            validate: value => value.Length switch
            {
                0 => "A name is required.",
                _ when value.Contains('"') => "A name cannot contain a double quote.",
                _ => null
            });

        List<string> methods = [.. RequestEditingHelpers.HttpMethods];
        if (state.Method.Length > 0 && !methods.Contains(state.Method, StringComparer.Ordinal))
        {
            methods.Add(state.Method);
        }

        ChoiceField<string> method = new("Method", methods, methods,
            state.Method, value => state.Method = value);

        var uri = new TextField("URL", state.Uri, value => state.Uri = value,
            "https://api.example.com/resource",
            validate: value => RequestEditingHelpers.IsValidAbsoluteUrl(value)
                ? null
                : "Enter an absolute URL, for example https://api.example.com/resource.");

        List<string> authLabels = [NoAuth, .. auths.Select(auth => auth.Name)];
        List<Guid?> authValues = [null, .. auths.Select(auth => (Guid?)auth.Id)];
        if (state.AuthId is { } authId && !authValues.Contains(authId))
        {
            authLabels.Add($"{authId.ToString()[..8]} · not in this workspace");
            authValues.Add(authId);
        }

        ChoiceField<Guid?> auth = new("Auth", authLabels, authValues, state.AuthId,
            value => state.AuthId = value);

        name.PendingEcho = TuiKeybindHelpers.OpeningEcho;
        _headers = new KeyValueField("Headers", "header", state.Headers);
        var body = new BodyFields(
            () => state.BodyType,
            value => state.BodyType = value,
            state.Bodies,
            state.Headers,
            () => _headers.Reload(),
            editContent,
            "This request sends no body. Choose a type above to give it one.");

        _view = new ResourceEditorView(
            () => state.Name.Length == 0 ? "new request" : SecretFormatting.Display(state.Name),
            () => workspaceName,
            BuildSummary(),
            isNew,
            [
                new EditorForm("Request", name, method, uri, auth),
                new EditorForm("Headers", _headers),
                new EditorForm("Params", new KeyValueField("Parameters", "parameter", state.Params)),
                new EditorForm("Body", body.Fields)
            ],
            save,
            closed,
            () => !_state.Matches(_opened),
            sourceName);
    }

    public void Show() => _view.Show();

    public void Update()
    {
        SyncSummary();
        _view.Update();
    }

    private void SyncSummary()
    {
        _summaryMethod.Value = _state.Method;
        _summaryUri.Value = _state.Uri.Length == 0
            ? string.Empty
            : SecretFormatting.Display(_state.GetDisplayUri());
    }

    public void Saved()
    {
        _opened = _state.Copy();
        _view.Saved();
    }

    public void Failed(string message) => _view.Failed(message);

    public void Suspend() => _view.Suspend();

    public void Report(string message, bool error) => _view.Report(message, error);

    private Visual BuildSummary() =>
        new HStack(
                new TextBlock(() => _summaryMethod.Value)
                    .Style(() => _summaryMethod.Value.Length == 0
                        ? StraumrStyleService.MutedText
                        : HttpMethodFormatting.Style(new HttpMethod(_summaryMethod.Value)))
                    .MinWidth(RequestEditingHelpers.HttpMethods.Max(value => value.Length)),
                new TextBlock(() => _summaryUri.Value.Length == 0 ? "no URL yet" : _summaryUri.Value)
                    .Style(() => _summaryUri.Value.Length == 0 ? StraumrStyleService.MutedText : StraumrStyleService.PrimaryText)
                    .Trimming(TextTrimming.EndEllipsis)
                    .HorizontalAlignment(Align.Stretch))
            .Spacing(2)
            .HorizontalAlignment(Align.Stretch);
}
