using Straumr.Core.Models;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Secret;

internal class SecretListEntryModel
{
    public StraumrSecret? Secret { get; init; }
    public required string Status { get; init; }
}
