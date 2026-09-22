using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Services;

internal static class SecretCatalogService
{
    private static string[] _names = [];

    public static void Set(IEnumerable<string> names) =>
        _names =
        [
            .. names
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        ];

    public static async Task LoadAsync(IStraumrSecretService secrets, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<StraumrSecret> loaded = await secrets.ListAsync(cancellationToken);
            Set(loaded.Select(secret => secret.Name));
        }
        catch (Exception exception) when (SecretScreen.IsRecoverable(exception))
        {
        }
    }

    public static IReadOnlyList<string> Match(string prefix) =>
        prefix.Length == 0
            ? _names
            :
            [
                .. _names
                    .Where(name => name.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            ];
}
