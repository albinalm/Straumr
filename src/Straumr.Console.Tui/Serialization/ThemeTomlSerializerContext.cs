using Tomlyn.Serialization;

namespace Straumr.Console.Tui.Serialization;

[TomlSerializable(typeof(ThemeDocumentModel))]
public partial class ThemeTomlSerializerContext : TomlSerializerContext;
