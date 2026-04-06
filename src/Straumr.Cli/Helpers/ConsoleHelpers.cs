using System.Text.Json;
using Spectre.Console;
using Straumr.Cli.Infrastructure;
using Straumr.Cli.Models;

namespace Straumr.Cli.Helpers;

internal static class ConsoleHelpers
{
    internal static void WriteError(string message, bool json)
    {
        if (json)
        {
            var envelope = new ErrorEnvelope(new ErrorDetail(message));
            System.Console.Error.WriteLine(JsonSerializer.Serialize(envelope, CliJsonContext.Relaxed.ErrorEnvelope));
        }
        else
        {
            System.Console.Error.WriteLine(message);
        }
    }
}
