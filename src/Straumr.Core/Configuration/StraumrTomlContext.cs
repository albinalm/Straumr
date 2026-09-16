using Straumr.Core.Models;
using Tomlyn.Serialization;

namespace Straumr.Core.Configuration;

/// <summary>
/// The source-generated TOML binder, for the same reason <see cref="StraumrJsonContext"/> exists:
/// the app publishes ahead of time, and reflection-based binding does not survive trimming.
/// </summary>
[TomlSerializable(typeof(StraumrSettings))]
public partial class StraumrTomlContext : TomlSerializerContext;
