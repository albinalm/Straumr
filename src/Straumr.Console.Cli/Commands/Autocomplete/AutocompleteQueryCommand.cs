using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Autocomplete;

[UsedImplicitly]
public sealed class AutocompleteQueryCommand(IServiceProvider services)
    : AsyncCommand<AutocompleteQueryCommandSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        AutocompleteQueryCommandSettings settings,
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
}
