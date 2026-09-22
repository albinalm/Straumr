using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Request;

[UsedImplicitly]
public sealed class RequestCreateCommandSettings : CommandSettings
{
    [CommandArgument(0, "[Name]")]
    [Description("Name of the request to create")]
    public string? Name { get; [UsedImplicitly] set; }

    [CommandArgument(1, "[Url]")]
    [Description("Request URL. When provided, creates the request directly without interactive prompts.")]
    public string? Url { get; [UsedImplicitly] set; }

    [CommandOption("-m|--method")]
    [Description("HTTP method (default: GET)")]
    public string? Method { get; [UsedImplicitly] set; }

    [CommandOption("-H|--header")]
    [Description("Header in \"Name: Value\" format (repeatable)")]
    public string[]? Headers { get; [UsedImplicitly] set; }

    [CommandOption("-P|--param")]
    [Description("Query param in \"key=value\" format (repeatable)")]
    public string[]? Params { get; [UsedImplicitly] set; }

    [CommandOption("-d|--data")]
    [Description("Request body content")]
    public string? Data { get; [UsedImplicitly] set; }

    [CommandOption("-t|--type")]
    [Description("Body type: json, xml, text, form, multipart, raw (default: json)")]
    public string? BodyType { get; [UsedImplicitly] set; }

    [CommandOption("-a|--auth")]
    [Description("Auth name or ID to link")]
    public string? Auth { get; [UsedImplicitly] set; }

    [CommandOption("-e|--editor")]
    [Description("Open the request in the default editor instead of interactive prompts")]
    public bool UseEditor { get; [UsedImplicitly] set; }

    [CommandOption("-j|--json")]
    [Description("Output the created request as JSON")]
    public bool Json { get; [UsedImplicitly] set; }

    [CommandOption("-w|--workspace")]
    [Description("Target workspace name or ID (overrides the current workspace for this command)")]
    public string? Workspace { get; [UsedImplicitly] set; }
}
