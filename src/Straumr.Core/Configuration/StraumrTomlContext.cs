using Tomlyn.Serialization;

namespace Straumr.Core.Configuration;

[TomlSerializable(typeof(StraumrSettings))]
public partial class StraumrTomlContext : TomlSerializerContext;
