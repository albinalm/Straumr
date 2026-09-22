using Straumr.Core.Enums;
using Straumr.Core.Models;

namespace Straumr.Console.Cli.Commands.Request;

internal sealed class RequestCreateStateModel(string name)
{
    public string Name { get; set; } = name;
    public string Uri { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public Dictionary<string, string> Params { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public BodyType BodyType { get; set; } = BodyType.None;
    public Dictionary<BodyType, string> Bodies { get; } = new();
    public Guid? AuthId { get; set; }

    public StraumrRequest ToRequest() =>
        new()
        {
            Name = Name,
            Uri = Uri,
            Method = new HttpMethod(Method),
            Params = Params,
            Headers = Headers,
            BodyType = BodyType,
            Bodies = Bodies,
            AuthId = AuthId
        };
}
