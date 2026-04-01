using Straumr.Core.Enums;

namespace Straumr.Core.Models;

public class CustomAuthConfig : StraumrAuthConfig
{
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "POST";
    public BodyType BodyType { get; set; } = BodyType.None;
    public Dictionary<BodyType, string> Bodies { get; set; } = new();
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Params { get; set; } = new(StringComparer.Ordinal);

    public ExtractionSource Source { get; set; } = ExtractionSource.JsonPath;
    public string ExtractionExpression { get; set; } = string.Empty;

    public string ApplyHeaderName { get; set; } = "Authorization";
    public string ApplyHeaderTemplate { get; set; } = "Bearer {{value}}";

    public string? CachedValue { get; set; }

    public override AuthType Type => AuthType.Custom;
}