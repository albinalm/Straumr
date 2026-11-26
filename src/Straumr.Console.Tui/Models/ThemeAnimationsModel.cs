using Tomlyn.Serialization;

namespace Straumr.Console.Tui.Models;

public sealed class ThemeAnimationsModel
{
    [TomlPropertyName("enabled")]
    public bool? Enabled { get; set; }

    [TomlPropertyName("spinner")]
    public bool? Spinner { get; set; }

    [TomlPropertyName("pulse")]
    public bool? Pulse { get; set; }

    [TomlPropertyName("transitions")]
    public bool? Transitions { get; set; }
}
