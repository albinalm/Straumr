using Microsoft.Extensions.DependencyInjection;
using Straumr.Core.Extensions;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Cli.Commands.Autocomplete;

public static class AutocompleteRunner
{
    public static async Task<int?> TryRunAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        int offset = args.Length > 0 &&
                     CompletionCatalog.IsCliPrefix(args[0])
            ? 1
            : 0;

        if (args.Length < offset + 2 ||
            !args[offset].Equals(CompletionCatalog.Autocomplete, StringComparison.OrdinalIgnoreCase) ||
            !args[offset + 1].Equals(CompletionCatalog.Query, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string query = string.Join(' ', args.Skip(offset + 2));
        if (AutocompleteEngine.TryCompleteStatic(query, out IReadOnlyList<string> staticCompletions))
        {
            Write(staticCompletions);
            return 0;
        }

        ServiceCollection services = new();
        services.AddStraumrCore();
        await using ServiceProvider provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IStraumrOptionsService>().LoadAsync(cancellationToken);

        AutocompleteEngine engine = new(provider);
        IReadOnlyList<string> completions = await engine.CompleteAsync(query, cancellationToken);
        Write(completions);
        return 0;
    }

    private static void Write(IEnumerable<string> completions)
    {
        foreach (string completion in completions)
        {
            System.Console.WriteLine(completion);
        }
    }
}
