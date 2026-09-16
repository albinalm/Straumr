using Tomlyn.Serialization;

namespace Straumr.Console.Tui.Visuals.Theming;

/// <summary>
/// A theme file as it is written: a name and a table of colours keyed by role.
/// </summary>
/// <remarks>
/// The colours are a dictionary rather than eighteen properties so the file can be forgiving about
/// how a key is spelled — <c>selection-inactive</c>, <c>selection_inactive</c> and
/// <c>SelectionInactive</c> are the same key — and so an unknown key can be named in the error
/// rather than silently dropped. Both matter for a file people keep in a dotfile repo.
/// <para>
/// Every key is named explicitly: Tomlyn binds by the property's own name, so an unattributed
/// <c>Colors</c> would look for <c>[Colors]</c> and quietly find nothing in a file written the way
/// TOML is conventionally written.
/// </para>
/// </remarks>
public sealed class ThemeDocument
{
    [TomlPropertyName("name")]
    public string? Name { get; set; }

    [TomlPropertyName("colors")]
    public Dictionary<string, string> Colors { get; set; } = [];

    /// <summary>
    /// A colour per HTTP method. Optional: a file that omits the table gets the semantic roles the
    /// methods have always borrowed, so every theme written before this existed still reads.
    /// </summary>
    [TomlPropertyName("methods")]
    public Dictionary<string, string> Methods { get; set; } = [];
}

[TomlSerializable(typeof(ThemeDocument))]
public partial class ThemeTomlContext : TomlSerializerContext;
