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
            var envelope = new CliErrorMessage(new CliErrorMessageContent(message));
            System.Console.Error.WriteLine(JsonSerializer.Serialize(envelope, CliJsonContext.Relaxed.CliErrorMessage));
        }
        else
        {
            System.Console.Error.WriteLine(message);
        }
    }
}
