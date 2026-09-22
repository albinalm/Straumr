using Straumr.Core.Enums;
using Straumr.Core.Models;

namespace Straumr.Console.Cli.Commands.Request;

internal sealed class RequestEditStateModel
{
    private RequestEditStateModel(string name, string uri, string method, Dictionary<string, string> parameters,
        Dictionary<string, string> headers, Dictionary<BodyType, string> bodies, BodyType bodyType,
        Guid? authId)
    {
        Name = name;
        Uri = uri;
        Method = method;
        Params = parameters;
        Headers = headers;
        Bodies = bodies;
        BodyType = bodyType;
        AuthId = authId;
    }

    public string Name { get; set; }
    public string Uri { get; set; }
    public string Method { get; set; }
    public Dictionary<string, string> Params { get; }
    public Dictionary<string, string> Headers { get; }
    public Dictionary<BodyType, string> Bodies { get; }
    public BodyType BodyType { get; set; }
    public Guid? AuthId { get; set; }

    public static RequestEditStateModel FromRequest(StraumrRequest request) =>
        new(
            request.Name,
            request.Uri,
            request.Method.Method,
            new Dictionary<string, string>(request.Params, StringComparer.Ordinal),
            new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase),
            new Dictionary<BodyType, string>(request.Bodies),
            request.BodyType,
            request.AuthId);

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
