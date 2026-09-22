using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Request;

[UsedImplicitly]
public sealed class RequestSendCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Name or ID>")]
    [Description("Name or ID of the request to send")]
    public required string Identifier { get; set; }

    [CommandOption("-v")]
    [Description("Show verbose output including request details")]
    public bool Verbose { get; [UsedImplicitly] set; }

    [CommandOption("-p|--pretty")]
    [Description("Format the response body for readability")]
    public bool Pretty { get; [UsedImplicitly] set; }

    [CommandOption("-b|--beautify")]
    [Description("Beautify JSON or XML response body")]
    public bool Beautify { get; [UsedImplicitly] set; }

    [CommandOption("-k|--insecure")]
    [Description("Allow insecure SSL/TLS connections")]
    public bool Insecure { get; [UsedImplicitly] set; }

    [CommandOption("-L|--location")]
    [Description("Follow HTTP redirects")]
    public bool FollowRedirects { get; [UsedImplicitly] set; }

    [CommandOption("-o|--output")]
    [Description("Write response body to file at the given path")]
    public string? OutputFile { get; [UsedImplicitly] set; }

    [CommandOption("-f|--fail")]
    [Description("Exit with a non-zero code on HTTP error responses (4xx/5xx)")]
    public bool Fail { get; [UsedImplicitly] set; }

    [CommandOption("-i|--include")]
    [Description("Include response headers in the output")]
    public bool IncludeHeaders { get; [UsedImplicitly] set; }

    [CommandOption("-s|--silent")]
    [Description("Suppress all output")]
    public bool Silent { get; [UsedImplicitly] set; }

    [CommandOption("-j|--json")]
    [Description("Output response as a JSON envelope {Status, Reason, Version, DurationMs, Headers, Body}")]
    public bool Json { get; [UsedImplicitly] set; }

    [CommandOption("-n|--dry-run")]
    [Description("Show the resolved request without sending it")]
    public bool DryRun { get; [UsedImplicitly] set; }

    [CommandOption("--response-status")]
    [Description("Output only the HTTP status code")]
    public bool ResponseStatus { get; [UsedImplicitly] set; }

    [CommandOption("--response-headers")]
    [Description("Output only the response headers")]
    public bool ResponseHeaders { get; [UsedImplicitly] set; }

    [CommandOption("-H|--header")]
    [Description("Add or override a header for this send only in \"Name: Value\" format (repeatable)")]
    public string[]? SendHeaders { get; [UsedImplicitly] set; }

    [CommandOption("-P|--param")]
    [Description("Add or override a query param for this send only in \"key=value\" format (repeatable)")]
    public string[]? SendParams { get; [UsedImplicitly] set; }

    [CommandOption("-w|--workspace")]
    [Description("Target workspace name or ID (overrides the current workspace for this command)")]
    public string? Workspace { get; [UsedImplicitly] set; }
}
