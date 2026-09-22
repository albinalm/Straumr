using Straumr.Core.Helpers;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Helpers;

public static class SecretReferenceHelpers
{
    public static async Task<IReadOnlyList<SecretReferenceModel>> ResolveAsync(
        IStraumrSecretService secrets,
        IEnumerable<string> documents,
        Func<Exception, bool> isRecoverable,
        CancellationToken cancellationToken)
    {
        List<SecretReferenceModel> references = new();
        foreach (string name in documents
                     .SelectMany(document => SecretHelpers.SecretPattern.Matches(document)
                         .Select(match => match.Groups["name"].Value))
                     .Distinct(StringComparer.Ordinal))
        {
            try
            {
                await secrets.GetAsync(name, false, cancellationToken);
                references.Add(new SecretReferenceModel(name, true));
            }
            catch (Exception exception) when (isRecoverable(exception))
            {
                references.Add(new SecretReferenceModel(name, false));
            }
        }

        return references;
    }
}
