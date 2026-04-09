using System.Text.Json.Serialization;
using Straumr.Console.Shared.Theme;

namespace Straumr.Console.Shared.Infrastructure;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(StraumrThemeOptions))]
public partial class StraumrConsoleSharedJsonContext : JsonSerializerContext;
