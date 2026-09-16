using Straumr.Console.Shared.Helpers;
using Straumr.Core.Enums;

namespace Straumr.Console.Tui.Visuals.Shared.Editor;

/// <summary>
/// The fields a body page is made of: the type, the document, and the two field maps a form body is
/// edited as. A request has one and a custom auth contains a whole request, so both have this.
/// </summary>
/// <remarks>
/// It is here rather than in either screen because a body is a body: the same types, the same
/// <c>Content-Type</c> the type implies, the same document handed to <c>$EDITOR</c> under the same
/// extension, and the same rule that each type keeps its own content so looking at XML does not
/// throw away the JSON.
/// </remarks>
internal sealed class BodyFields
{
    private readonly Func<BodyType> _type;
    private readonly Action<BodyType> _setType;
    private readonly IDictionary<BodyType, string> _bodies;
    private readonly IDictionary<string, string> _headers;
    private readonly Action _headersChanged;
    private readonly ContentField _text;
    private readonly Dictionary<string, string> _formFields;
    private readonly Dictionary<string, string> _multipartFields;

    /// <summary>
    /// Which type <see cref="_text"/> currently holds the content of, which is not the resource's
    /// body type while that type is a field body or none at all.
    /// </summary>
    private BodyType _textType;

    /// <param name="headersChanged">
    /// Run after the <c>Content-Type</c> header is rewritten, so the page showing the headers
    /// re-reads a map that was changed behind its back.
    /// </param>
    /// <param name="emptyMessage">
    /// What the page says when the type is <see cref="BodyType.None"/>. Only the caller knows what
    /// the thing sending no body is called.
    /// </param>
    public BodyFields(
        Func<BodyType> type,
        Action<BodyType> setType,
        IDictionary<BodyType, string> bodies,
        IDictionary<string, string> headers,
        Action headersChanged,
        Action<ExternalContentEdit> editContent,
        string emptyMessage)
    {
        _type = type;
        _setType = setType;
        _bodies = bodies;
        _headers = headers;
        _headersChanged = headersChanged;
        _textType = RequestEditingHelpers.IsFieldBody(type()) || type() == BodyType.None
            ? BodyType.Json
            : type();

        _formFields = RequestEditingHelpers.ParseQueryString(
            bodies.TryGetValue(BodyType.FormUrlEncoded, out string? form) ? form : null, StringComparer.Ordinal);
        _multipartFields = RequestEditingHelpers.ParseQueryString(
            bodies.TryGetValue(BodyType.MultipartForm, out string? parts) ? parts : null, StringComparer.Ordinal);

        var typeField = new ChoiceField<BodyType>("Type",
            RequestEditingHelpers.BodyTypes.Select(RequestEditingHelpers.BodyTypeDisplayName).ToList(),
            RequestEditingHelpers.BodyTypes,
            type(),
            SetType);

        _text = new ContentField("Content",
            bodies.TryGetValue(_textType, out string? content) ? content : string.Empty,
            StoreText,
            ContentFormatFor(_textType),
            editContent)
        {
            Visible = () => IsTextBody(_type())
        };

        var formBody = new KeyValueField("Fields", "field", _formFields)
        {
            Visible = () => _type() == BodyType.FormUrlEncoded,
            OnCommit = () => Store(BodyType.FormUrlEncoded, _formFields)
        };

        var multipartBody = new KeyValueField("Parts", "part", _multipartFields, KeyValueValueKind.TextOrFile)
        {
            Visible = () => _type() == BodyType.MultipartForm,
            OnCommit = () => Store(BodyType.MultipartForm, _multipartFields)
        };

        var noBody = new MessageField("Content", emptyMessage)
        {
            Visible = () => _type() == BodyType.None
        };

        Fields = [typeField, _text, formBody, multipartBody, noBody];
    }

    public EditorField[] Fields { get; }

    private static bool IsTextBody(BodyType type) =>
        type != BodyType.None && !RequestEditingHelpers.IsFieldBody(type);

    /// <remarks>
    /// The header follows the type, as it does in the CLI: a body the server cannot identify is a
    /// body the request did not really carry. It is written rather than offered, and the Headers page
    /// is where it can be overridden afterwards.
    /// </remarks>
    private void SetType(BodyType type)
    {
        if (IsTextBody(_type()))
            StoreText(_text.Value);

        _setType(type);
        RequestEditingHelpers.SyncContentTypeHeader(_headers, type);
        _headersChanged();

        if (!IsTextBody(type))
            return;

        _textType = type;
        _text.Set(_bodies.TryGetValue(type, out string? content) ? content : string.Empty);
        _text.UseFormat(ContentFormatFor(type));
    }

    /// <summary>
    /// How a body of this type is handed to an external editor: under the extension that tells the
    /// editor its language, and opened on something worth opening on rather than a blank file.
    /// </summary>
    private static ContentFormat ContentFormatFor(BodyType type) =>
        new(RequestEditingHelpers.BodyTypeFileExtension(type),
            body => RequestEditingHelpers.BodyEditingDocument(type, body));

    private void StoreText(string value)
    {
        if (!IsTextBody(_type()))
            return;
        Store(_textType, value);
    }

    private void Store(BodyType type, IReadOnlyDictionary<string, string> fields) =>
        Store(type, RequestEditingHelpers.BuildQueryString(fields));

    /// <remarks>
    /// An empty body is an absent one. Leaving the key behind with an empty string would persist a
    /// body the resource does not have, and Core already treats a blank one as none.
    /// </remarks>
    private void Store(BodyType type, string value)
    {
        if (string.IsNullOrEmpty(value))
            _bodies.Remove(type);
        else
            _bodies[type] = value;
    }
}
