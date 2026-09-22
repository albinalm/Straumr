using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Request;

[UsedImplicitly]
public sealed class RequestEditCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Name or ID>")]
    [Description("Name or ID of the request to edit")]
    public required string Identifier { get; set; }

    [CommandOption("-e|--editor")]
    [Description("Open the request in the default editor instead of interactive prompts")]
    public bool UseEditor { get; [UsedImplicitly] set; }

    [CommandOption("-n|--name")]
    [Description("The new name for the request")]
    public string? Name { get; [UsedImplicitly] set; }

    [CommandOption("-u|--url")]
    [Description("New URL for the request")]
    public string? Url { get; [UsedImplicitly] set; }

    [CommandOption("-m|--method")]
    [Description("New HTTP method")]
    public string? Method { get; [UsedImplicitly] set; }

    [CommandOption("-H|--header")]
    [Description("Add or override a header in \"Name: Value\" format (repeatable)")]
    public string[]? Headers { get; [UsedImplicitly] set; }

    [CommandOption("-P|--param")]
    [Description("Add or override a query param in \"key=value\" format (repeatable)")]
    public string[]? Params { get; [UsedImplicitly] set; }

    [CommandOption("-d|--data")]
    [Description("New request body content")]
    public string? Data { get; [UsedImplicitly] set; }

    [CommandOption("-t|--type")]
    [Description("Body type: json, xml, text, form, multipart, raw, none")]
    public string? BodyType { get; [UsedImplicitly] set; }

    [CommandOption("-a|--auth")]
    [Description("Auth name or ID to link (use \"none\" to remove auth)")]
    public string? Auth { get; [UsedImplicitly] set; }

    [CommandOption("-w|--workspace")]
    [Description("Target workspace name or ID (overrides the current workspace for this command)")]
    public string? Workspace { get; [UsedImplicitly] set; }

    [CommandOption("-j|--json")]
    [Description("Output the updated request as JSON; implies --editor when no inline flags are set")]
    public bool Json { get; [UsedImplicitly] set; }
}
