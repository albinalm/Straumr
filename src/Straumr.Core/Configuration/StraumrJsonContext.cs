using System.Text.Json.Serialization;
using Straumr.Core.Models;

namespace Straumr.Core.Configuration;

[JsonSourceGenerationOptions(WriteIndented = true )]
[JsonSerializable(typeof(StraumrWorkspace))]
[JsonSerializable(typeof(StraumrRequest))]
[JsonSerializable(typeof(StraumrOptions))]
public partial class StraumrJsonContext : JsonSerializerContext;
