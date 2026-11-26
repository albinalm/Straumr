using Tomlyn.Serialization;

namespace Straumr.Console.Tui.Models;

public sealed class ThemeDocumentModel
{
    [TomlPropertyName("name")]
    public string? Name { get; set; }

    [TomlPropertyName("layout")]
    public string? Layout { get; set; }

    [TomlPropertyName("colors")]
    public Dictionary<string, string> Colors { get; set; } = [];

    [TomlPropertyName("methods")]
    public Dictionary<string, string> Methods { get; set; } = [];

    [TomlPropertyName("code")]
    public Dictionary<string, string> Code { get; set; } = [];

    [TomlPropertyName("animations")]
    public ThemeAnimationsModel? Animations { get; set; }

    [TomlPropertyName("gradient")]
    public ThemeGradientModel? Gradient { get; set; }
}
