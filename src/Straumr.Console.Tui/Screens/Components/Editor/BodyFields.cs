using Straumr.Core.Enums;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class BodyFields
{
    private readonly IDictionary<BodyType, string> _bodies;
    private readonly Dictionary<string, string> _formFields;
    private readonly IDictionary<string, string> _headers;
    private readonly Action _headersChanged;
    private readonly Dictionary<string, string> _multipartFields;
    private readonly Action<BodyType> _setType;
    private readonly ContentField _text;
    private readonly Func<BodyType> _type;

    private BodyType _textType;

    public BodyFields(
        Func<BodyType> type,
        Action<BodyType> setType,
        IDictionary<BodyType, string> bodies,
        IDictionary<string, string> headers,
        Action headersChanged,
        Action<ExternalContentEditModel> editContent,
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

        ChoiceField<BodyType> typeField = new("Type",
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

    private void SetType(BodyType type)
    {
        if (IsTextBody(_type()))
        {
            StoreText(_text.Value);
        }

        _setType(type);
        RequestEditingHelpers.SyncContentTypeHeader(_headers, type);
        _headersChanged();

        if (!IsTextBody(type))
        {
            return;
        }

        _textType = type;
        _text.Set(_bodies.TryGetValue(type, out string? content) ? content : string.Empty);
        _text.UseFormat(ContentFormatFor(type));
    }

    private static ContentFormatModel ContentFormatFor(BodyType type) =>
        new(RequestEditingHelpers.BodyTypeFileExtension(type),
            body => RequestEditingHelpers.BodyEditingDocument(type, body));

    private void StoreText(string value)
    {
        if (!IsTextBody(_type()))
        {
            return;
        }

        Store(_textType, value);
    }

    private void Store(BodyType type, IReadOnlyDictionary<string, string> fields) =>
        Store(type, RequestEditingHelpers.BuildQueryString(fields));

    private void Store(BodyType type, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            _bodies.Remove(type);
        }
        else
        {
            _bodies[type] = value;
        }
    }
}
