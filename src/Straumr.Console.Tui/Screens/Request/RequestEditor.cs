using Straumr.Console.Shared.Helpers;
using Straumr.Console.Shared.Models;
using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Console.Tui.Visuals.Shared.Editor;
using Straumr.Core.Enums;
using Straumr.Core.Models;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Request;

/// <summary>
/// What a request looks like in the shared editor: the pages, the fields on them, and the rules
/// about which of them apply. Everything else about editing a request — the screen it opens on, the
/// bar, the save gesture, validation display — belongs to the kit and is not repeated here.
/// </summary>
/// <remarks>
/// This is the whole of what an editable resource has to supply. An auth's four configuration
/// shapes and a secret's two fields are files of this size beside it, over the same kit.
/// </remarks>
internal sealed class RequestEditor
{
    private const string NoAuth = "None";

    private readonly RequestEditorState _state;
    private readonly ResourceEditorView _view;

    /// <summary>
    /// Each body type keeps its own content, so switching type has to put away what the editor was
    /// holding and fetch what the new type left behind. Without it, choosing XML to look at it and
    /// choosing JSON again would have overwritten the JSON with the XML.
    /// </summary>
    private readonly ContentField _bodyText;

    private readonly Dictionary<string, string> _formFields;
    private readonly Dictionary<string, string> _multipartFields;

    /// <summary>Held because choosing a body type writes into the headers behind this field's back.</summary>
    private readonly KeyValueField _headers;

    private BodyType _bodyTextType;

    /// <summary>
    /// The bar's two halves, mirrored out of the state once per update pass.
    /// </summary>
    /// <remarks>
    /// A visual's dynamic text is re-read when something it read changes, and what these read is the
    /// state being edited, which is a plain object the binding graph knows nothing about: the bar
    /// went on saying <c>GET</c> after the method had been changed to <c>PUT</c>. Its colour did
    /// follow, which is what made the staleness so odd to look at — a style given as a function is a
    /// factory the renderer calls every frame, not a binding. Copying into state the graph does know
    /// about is what puts the two back in step, and leaves every binding over them working normally.
    /// </remarks>
    private readonly State<string> _summaryMethod;

    /// <summary>The URL as it will be sent, parameters folded in; empty when there is none yet.</summary>
    private readonly State<string> _summaryUri;

    /// <param name="openingGesture">
    /// The key that opened the editor, discarded by the field that takes initial focus. A printable
    /// gesture arrives as a key event and an independent text event, so <c>c</c> would otherwise type
    /// itself into the name.
    /// </param>
    /// <param name="editContent">
    /// Where the body goes to be written. The screen owns it because running another program means
    /// putting the terminal down, which is not something a form can do from inside a keystroke.
    /// </param>
    public RequestEditor(
        RequestEditorState state,
        IReadOnlyList<StraumrAuth> auths,
        string? workspaceName,
        bool isNew,
        char? openingGesture,
        Action save,
        Action closed,
        Action<ExternalContentEdit> editContent)
    {
        _state = state;
        _summaryMethod = new State<string>(state.Method);
        _summaryUri = new State<string>(state.Uri.Length == 0 ? string.Empty : state.GetDisplayUri());
        _bodyTextType = RequestEditingHelpers.IsFieldBody(state.BodyType) || state.BodyType == BodyType.None
            ? BodyType.Json
            : state.BodyType;

        _formFields = RequestEditingHelpers.ParseQueryString(
            state.Bodies.GetValueOrDefault(BodyType.FormUrlEncoded), StringComparer.Ordinal);
        _multipartFields = RequestEditingHelpers.ParseQueryString(
            state.Bodies.GetValueOrDefault(BodyType.MultipartForm), StringComparer.Ordinal);

        var name = new TextField("Name", state.Name, value => state.Name = value,
            placeholder: "request name",
            validate: value => value.Length switch
            {
                0 => "A name is required.",
                // Core refuses it on save; saying so here costs a keystroke rather than a round trip.
                _ when value.Contains('"') => "A name cannot contain a double quote.",
                _ => null
            });

        // A method the list does not offer is still the one this request uses, so it joins the list
        // rather than being quietly replaced by the first entry the moment the editor opens.
        List<string> methods = [.. RequestEditingHelpers.HttpMethods];
        if (state.Method.Length > 0 && !methods.Contains(state.Method, StringComparer.Ordinal))
            methods.Add(state.Method);

        var method = new ChoiceField<string>("Method", methods, methods,
            state.Method, value => state.Method = value);

        var uri = new TextField("URL", state.Uri, value => state.Uri = value,
            placeholder: "https://api.example.com/resource",
            validate: value => RequestEditingHelpers.IsValidAbsoluteUrl(value)
                ? null
                : "Enter an absolute URL, for example https://api.example.com/resource.");

        List<string> authLabels = [NoAuth, .. auths.Select(auth => auth.Name)];
        List<Guid?> authValues = [null, .. auths.Select(auth => (Guid?)auth.Id)];
        // An auth the workspace no longer holds is still what this request references, so it stays
        // selectable by its id rather than silently becoming None the moment the editor opens.
        if (state.AuthId is { } authId && !authValues.Contains(authId))
        {
            authLabels.Add($"{authId.ToString()[..8]} · not in this workspace");
            authValues.Add(authId);
        }

        var auth = new ChoiceField<Guid?>("Auth", authLabels, authValues, state.AuthId,
            value => state.AuthId = value);

        var bodyType = new ChoiceField<BodyType>("Type",
            RequestEditingHelpers.BodyTypes.Select(RequestEditingHelpers.BodyTypeDisplayName).ToList(),
            RequestEditingHelpers.BodyTypes,
            state.BodyType,
            SetBodyType);

        _bodyText = new ContentField("Content",
            state.Bodies.GetValueOrDefault(_bodyTextType, string.Empty),
            value => StoreBodyText(value),
            ContentFormatFor(_bodyTextType),
            editContent)
        {
            Visible = () => IsTextBody(state.BodyType)
        };

        var formBody = new KeyValueField("Fields", "field", _formFields)
        {
            Visible = () => state.BodyType == BodyType.FormUrlEncoded,
            OnCommit = () => Store(BodyType.FormUrlEncoded, _formFields)
        };

        var multipartBody = new KeyValueField("Parts", "part", _multipartFields, KeyValueValueKind.TextOrFile)
        {
            Visible = () => state.BodyType == BodyType.MultipartForm,
            OnCommit = () => Store(BodyType.MultipartForm, _multipartFields)
        };

        var noBody = new MessageField("Content", "This request sends no body. Choose a type above to give it one.")
        {
            Visible = () => state.BodyType == BodyType.None
        };

        name.PendingEcho = openingGesture;
        _headers = new KeyValueField("Headers", "header", state.Headers);

        _view = new ResourceEditorView(
            () => state.Name.Length == 0 ? "new request" : state.Name,
            () => workspaceName,
            BuildSummary(),
            isNew,
            [
                new EditorForm("Request", name, method, uri, auth),
                new EditorForm("Headers", _headers),
                new EditorForm("Params", new KeyValueField("Parameters", "parameter", state.Params)),
                new EditorForm("Body", bodyType, _bodyText, formBody, multipartBody, noBody)
            ],
            save,
            closed);
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
        _summaryUri.Value = _state.Uri.Length == 0 ? string.Empty : _state.GetDisplayUri();
    }

    public void Saved() => _view.Saved();

    public void Failed(string message) => _view.Failed(message);

    public void Suspend() => _view.Suspend();

    public void Report(string message, bool error) => _view.Report(message, error);

    /// <summary>
    /// The bar's left half, live: the method and the URL with its configured parameters folded in,
    /// which is the line the request will actually be sent on and the pair worth checking before
    /// saving. It re-reads the state rather than the fields, so the Params page shows up here too.
    /// </summary>
    private Visual BuildSummary() =>
        new HStack(
                new TextBlock(() => _summaryMethod.Value)
                    .Style(() => _summaryMethod.Value.Length == 0
                        ? StraumrStyles.MutedText
                        : HttpMethodFormatting.Style(new HttpMethod(_summaryMethod.Value)))
                    .MinWidth(RequestEditingHelpers.HttpMethods.Max(value => value.Length)),
                new TextBlock(() => _summaryUri.Value.Length == 0 ? "no URL yet" : _summaryUri.Value)
                    .Style(() => _summaryUri.Value.Length == 0 ? StraumrStyles.MutedText : StraumrStyles.PrimaryText)
                    .Trimming(TextTrimming.EndEllipsis)
                    .HorizontalAlignment(Align.Stretch))
            .Spacing(2)
            .HorizontalAlignment(Align.Stretch);

    private static bool IsTextBody(BodyType type) =>
        type != BodyType.None && !RequestEditingHelpers.IsFieldBody(type);

    /// <remarks>
    /// The header follows the type, as it does in the CLI: a body the server cannot identify is a
    /// body the request did not really carry. It is written rather than offered, and the Headers page
    /// is where it can be overridden afterwards.
    /// </remarks>
    private void SetBodyType(BodyType type)
    {
        if (IsTextBody(_state.BodyType))
            StoreBodyText(_bodyText.Value);

        _state.BodyType = type;
        RequestEditingHelpers.SyncContentTypeHeader(_state.Headers, type);
        _headers.Reload();

        if (!IsTextBody(type))
            return;

        _bodyTextType = type;
        _bodyText.Set(_state.Bodies.GetValueOrDefault(type, string.Empty));
        _bodyText.UseFormat(ContentFormatFor(type));
    }

    /// <summary>
    /// How a body of this type is handed to an external editor: under the extension that tells the
    /// editor its language, and opened on something worth opening on rather than a blank file.
    /// </summary>
    private static ContentFormat ContentFormatFor(BodyType type) =>
        new(RequestEditingHelpers.BodyTypeFileExtension(type),
            body => RequestEditingHelpers.BodyEditingDocument(type, body));

    private void StoreBodyText(string value)
    {
        if (!IsTextBody(_state.BodyType))
            return;
        Store(_bodyTextType, value);
    }

    private void Store(BodyType type, IReadOnlyDictionary<string, string> fields) =>
        Store(type, RequestEditingHelpers.BuildQueryString(fields));

    /// <remarks>
    /// An empty body is an absent one. Leaving the key behind with an empty string would persist a
    /// body the request does not have, and Core already treats a blank one as none.
    /// </remarks>
    private void Store(BodyType type, string value)
    {
        if (string.IsNullOrEmpty(value))
            _state.Bodies.Remove(type);
        else
            _state.Bodies[type] = value;
    }
}
