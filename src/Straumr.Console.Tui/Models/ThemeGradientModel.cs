using Tomlyn.Serialization;

namespace Straumr.Console.Tui.Models;

public sealed class ThemeGradientModel
{
    [TomlPropertyName("stops")]
    public List<string>? Stops { get; set; }
}
