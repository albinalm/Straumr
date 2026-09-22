using Straumr.Core.Enums;
using Straumr.Core.Models;

namespace Straumr.Console.Shared.Models;

public sealed class RequestEditorStateModel
{

    private RequestEditorStateModel(string name)
    {
        Name = name;
        Uri = string.Empty;
        Method = "GET";
        Params = new Dictionary<string, string>(StringComparer.Ordinal);
        Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        BodyType = BodyType.None;
        Bodies = new Dictionary<BodyType, string>();
    }
    public string Name { get; set; }
    public string Uri { get; set; }
    public string Method { get; set; }
    public Dictionary<string, string> Params { get; }
    public Dictionary<string, string> Headers { get; }
    public BodyType BodyType { get; set; }
    public Dictionary<BodyType, string> Bodies { get; }
    public Guid? AuthId { get; set; }

    public static RequestEditorStateModel CreateNew() => new(string.Empty);

    public static RequestEditorStateModel FromRequest(StraumrRequest request)
    {
        RequestEditorStateModel state = new(request.Name)
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

    public RequestEditorStateModel Copy()
    {
        RequestEditorStateModel copy = new(Name)
        {
            Uri = Uri,
            Method = Method,
            BodyType = BodyType,
            AuthId = AuthId
        };

        foreach (KeyValuePair<string, string> kv in Params)
        {
            copy.Params[kv.Key] = kv.Value;
        }

        foreach (KeyValuePair<string, string> kv in Headers)
        {
            copy.Headers[kv.Key] = kv.Value;
        }

        foreach (KeyValuePair<BodyType, string> kv in Bodies)
        {
            copy.Bodies[kv.Key] = kv.Value;
        }

        return copy;
    }

    public bool Matches(RequestEditorStateModel other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return string.Equals(Name, other.Name, StringComparison.Ordinal) &&
               string.Equals(Uri, other.Uri, StringComparison.Ordinal) &&
               string.Equals(Method, other.Method, StringComparison.Ordinal) &&
               BodyType == other.BodyType &&
               AuthId == other.AuthId &&
               string.Equals(Body(this), Body(other), StringComparison.Ordinal) &&
               Same(Params, other.Params) &&
               Same(Headers, other.Headers);

        static string Body(RequestEditorStateModel state) =>
            state.Bodies.GetValueOrDefault(state.BodyType, string.Empty);

        static bool Same(Dictionary<string, string> left, Dictionary<string, string> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, string> kv in left)
            {
                if (!right.TryGetValue(kv.Key, out string? value) ||
                    !string.Equals(kv.Value, value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public string GetDisplayUri()
    {
        if (string.IsNullOrWhiteSpace(Uri) || Params.Count == 0)
        {
            return Uri;
        }

        try
        {
            UriBuilder builder = new(Uri);
            List<string> queryParts = [];

            if (!string.IsNullOrEmpty(builder.Query))
            {
                queryParts.Add(builder.Query.TrimStart('?'));
            }

            string paramsQuery = RequestEditingHelpers.BuildQueryString(Params);
            if (!string.IsNullOrEmpty(paramsQuery))
            {
                queryParts.Add(paramsQuery);
            }

            builder.Query = string.Join('&', queryParts);
            return builder.Uri.ToString();
        }
        catch
        {
            return Uri;
        }
    }

    public void SetDisplayUri(string value)
    {
        UriBuilder builder = new(value);

        Params.Clear();
        foreach (KeyValuePair<string, string> kv in RequestEditingHelpers.ParseQueryString(builder.Query, StringComparer.Ordinal))
        {
            Params[kv.Key] = kv.Value;
        }

        builder.Query = string.Empty;
        Uri = builder.Uri.ToString();
    }

    public StraumrRequest ToRequest() =>
        new()
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
