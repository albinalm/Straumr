using Straumr.Core.Enums;
using Straumr.Core.Models;

namespace Straumr.Console.Shared.Models;

public sealed class RequestEditorState
{
    public string Name { get; set; }
    public string Uri { get; set; }
    public string Method { get; set; }
    public Dictionary<string, string> Params { get; }
    public Dictionary<string, string> Headers { get; }
    public BodyType BodyType { get; set; }
    public Dictionary<BodyType, string> Bodies { get; }
    public Guid? AuthId { get; set; }

    private RequestEditorState(string name)
    {
        Name = name;
        Uri = string.Empty;
        Method = "GET";
        Params = new Dictionary<string, string>(StringComparer.Ordinal);
        Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        BodyType = BodyType.None;
        Bodies = new Dictionary<BodyType, string>();
    }

    public static RequestEditorState CreateNew() => new(string.Empty);

    public static RequestEditorState FromRequest(StraumrRequest request)
    {
        RequestEditorState state = new(request.Name)
        {
            Uri = request.Uri,
            Method = request.Method.Method,
            BodyType = request.BodyType,
            AuthId = request.AuthId
        };

        foreach (KeyValuePair<string, string> kv in request.Params)
        {
            state.Params[kv.Key] = kv.Value;
        }

        foreach (KeyValuePair<string, string> kv in request.Headers)
        {
            state.Headers[kv.Key] = kv.Value;
        }

        foreach (KeyValuePair<BodyType, string> kv in request.Bodies)
        {
            state.Bodies[kv.Key] = kv.Value;
        }

        return state;
    }

    public StraumrRequest ToRequest()
    {
        return new StraumrRequest
        {
            Name = Name,
            Uri = Uri,
            Method = new HttpMethod(Method),
            Params = new Dictionary<string, string>(Params, StringComparer.Ordinal),
            Headers = new Dictionary<string, string>(Headers, StringComparer.OrdinalIgnoreCase),
            BodyType = BodyType,
            Bodies = new Dictionary<BodyType, string>(Bodies),
            AuthId = AuthId
        };
    }

    public void ApplyTo(StraumrRequest request)
    {
        request.Name = Name;
        request.Uri = Uri;
        request.Method = new HttpMethod(Method);
        request.Params = new Dictionary<string, string>(Params, StringComparer.Ordinal);
        request.Headers = new Dictionary<string, string>(Headers, StringComparer.OrdinalIgnoreCase);
        request.BodyType = BodyType;
        request.Bodies = new Dictionary<BodyType, string>(Bodies);
        request.AuthId = AuthId;
    }
}
