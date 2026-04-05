using System.Text.Json.Serialization;
using Straumr.Cli.Models;

namespace Straumr.Cli.Infrastructure;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(WorkspaceListItem[]))]
[JsonSerializable(typeof(RequestListItem[]))]
[JsonSerializable(typeof(AuthListItem[]))]
[JsonSerializable(typeof(SecretListItem[]))]
[JsonSerializable(typeof(SendResult))]
[JsonSerializable(typeof(SendErrorEnvelope))]
[JsonSerializable(typeof(DryRunResult))]
public partial class CliJsonContext : JsonSerializerContext;
