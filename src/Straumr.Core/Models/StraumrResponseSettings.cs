using Tomlyn.Serialization;

namespace Straumr.Core.Models;

public sealed class StraumrResponseSettings
{
    [TomlPropertyName("format")]
    public string? Format { get; set; }

    [TomlPropertyName("highlight")]
    public bool? Highlight { get; set; }

    [TomlPropertyName("highlight-limit")]
    public int? HighlightLimit { get; set; }

    [TomlPropertyName("store-limit")]
    public int? StoreLimit { get; set; }
}
