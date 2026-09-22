using Straumr.Core.Models;

namespace Straumr.Console.Cli.Commands.Request;

internal class RequestListEntryModel
{
    public Guid Id { get; init; }
    public StraumrRequest? Request { get; init; }
    public required string Status { get; init; }
}
