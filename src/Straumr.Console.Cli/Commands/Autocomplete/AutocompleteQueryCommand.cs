using System.ComponentModel;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Autocomplete;

public sealed class AutocompleteQueryCommand(IServiceProvider services)
    : AsyncCommand<AutocompleteQueryCommand.Settings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        AutocompleteEngine engine = new(services);
        IReadOnlyList<string> completions =
            await engine.CompleteAsync(settings.Query, cancellationToken);

        foreach (string completion in completions)
        {
            System.Console.WriteLine(completion);
        }

        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Query>")]
        [Description("Current command line to complete")]
        public required string Query { get; set; }
    }
}
