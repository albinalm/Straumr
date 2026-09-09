using Straumr.Core.Enums;

namespace Straumr.Core.Models;

public class StraumrRequest : StraumrModelBase
{
    public required string Uri { get; set; }
    public required HttpMethod Method { get; set; }
    public Dictionary<string, string> Params { get; set; } = new();
    public Dictionary<string, string> Headers { get; set; } = new();
    public BodyType BodyType { get; set; } = BodyType.None;
    public Dictionary<BodyType, string> Bodies { get; set; } = new();
    public Guid? AuthId { get; set; }
    public string? Group { get; set; }

    public StraumrRequest CopyAs(string name) => new()
    {
        Name = name,
        Uri = Uri,
        Method = Method,
        Params = new Dictionary<string, string>(Params, Params.Comparer),
        Headers = new Dictionary<string, string>(Headers, Headers.Comparer),
        BodyType = BodyType,
        Bodies = new Dictionary<BodyType, string>(Bodies),
        AuthId = AuthId
    };
}
