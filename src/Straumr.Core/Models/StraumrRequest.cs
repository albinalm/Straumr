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
    public AuthType AuthType { get; set; } = AuthType.None;
    public BearerAuthConfig? BearerAuth { get; set; }
    public BasicAuthConfig? BasicAuth { get; set; }
    public OAuth2Config? OAuth2 { get; set; }
    public CustomAuthConfig? CustomAuth { get; set; }
}
