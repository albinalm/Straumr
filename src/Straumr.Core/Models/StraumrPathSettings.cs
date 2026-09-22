using Tomlyn.Serialization;

namespace Straumr.Core.Models;

public sealed class StraumrPathSettings
{
    [TomlPropertyName("workspaces")]
    public string? Workspaces { get; set; }

    [TomlPropertyName("secrets")]
    public string? Secrets { get; set; }
}
