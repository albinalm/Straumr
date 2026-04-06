using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Straumr.Cli.Models;

namespace Straumr.Cli.Infrastructure;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(WorkspaceListItem[]))]
[JsonSerializable(typeof(WorkspaceCreateResult))]
[JsonSerializable(typeof(RequestListItem[]))]
[JsonSerializable(typeof(RequestCreateResult))]
[JsonSerializable(typeof(RequestGetResult))]
[JsonSerializable(typeof(AuthListItem[]))]
[JsonSerializable(typeof(AuthListItem))]
[JsonSerializable(typeof(SecretListItem[]))]
[JsonSerializable(typeof(SendResult))]
[JsonSerializable(typeof(ErrorEnvelope))]
[JsonSerializable(typeof(DryRunResult))]
[JsonSerializable(typeof(ConfigWorkspacePathResult))]
[JsonSerializable(typeof(WorkspaceExportResult))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(string))]
public partial class CliJsonContext : JsonSerializerContext
{
    public static CliJsonContext Relaxed { get; } = new(
        new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
}
