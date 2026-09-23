using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Services;

internal static class VariableCatalogService
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

    public static async Task LoadAsync(IStraumrVariableService variables, StraumrWorkspaceEntry? workspace,
        CancellationToken cancellationToken)
    {
        if (workspace is null)
        {
            Set([]);
            return;
        }

        try
        {
            IReadOnlyList<StraumrVariable> loaded = await variables.ListAsync(workspace, cancellationToken);
            Set(loaded.Select(variable => variable.Name));
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
