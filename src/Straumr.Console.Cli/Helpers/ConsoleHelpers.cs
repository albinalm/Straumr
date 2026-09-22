using System.Text.Json;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;

namespace Straumr.Console.Cli.Helpers;

internal static class ConsoleHelpers
{
    internal static void WriteError(string message, bool json)
    {
        if (json)
        {
            var envelope = new CliErrorMessage(new CliErrorMessageContent(message));
            System.Console.Error.WriteLine(JsonSerializer.Serialize(envelope, CliJsonContext.Relaxed.CliErrorMessage));
        }
        else
        {
            System.Console.Error.WriteLine(message);
        }
    }
}
