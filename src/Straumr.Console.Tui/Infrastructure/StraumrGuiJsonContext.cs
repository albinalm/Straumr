using System.Text.Json.Serialization;
using Straumr.Console.Tui.Theme;

namespace Straumr.Console.Tui.Infrastructure;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(StraumrThemeOptions))]
public partial class StraumrGuiJsonContext : JsonSerializerContext;
