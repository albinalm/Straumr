using Straumr.Core.Helpers;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Infrastructure;

/// <summary>One <c>{{secret:name}}</c> a resource depends on, and whether the store can supply it.</summary>
public sealed record SecretReference(string Name, bool Available);

/// <summary>
/// Which secrets a resource refers to, and which of them exist. A request asks about itself and its
/// auth; an auth asks about itself. Both answer the same question — whether a send will fail before
/// it starts — so both ask it the same way.
/// </summary>
public static class SecretReferences
{
    /// <param name="documents">
    /// The serialized resources to scan. A reference is a literal in a stored value wherever it
    /// appears — a URL, a header, a client secret — so the whole document is the place to look for
    /// one rather than a list of the fields that happen to allow it today.
    /// </param>
    /// <remarks>
    /// Names are looked up without stamping access. Reading a screen is not using a secret, and a
    /// list of them that reordered the store every time it was drawn would be.
    /// </remarks>
    public static async Task<IReadOnlyList<SecretReference>> ResolveAsync(
        IStraumrSecretService secrets,
        IEnumerable<string> documents,
        Func<Exception, bool> isRecoverable,
        CancellationToken cancellationToken)
    {
        var references = new List<SecretReference>();
        foreach (string name in documents
                     .SelectMany(document => SecretHelpers.SecretPattern.Matches(document)
                         .Select(match => match.Groups["name"].Value))
                     .Distinct(StringComparer.Ordinal))
        {
            try
            {
                await secrets.GetAsync(name, updateLastAccessed: false, cancellationToken);
                references.Add(new SecretReference(name, Available: true));
            }
            catch (Exception exception) when (isRecoverable(exception))
            {
                references.Add(new SecretReference(name, Available: false));
            }
        }

        return references;
    }
}
