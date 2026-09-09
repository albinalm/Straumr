using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Cli.Commands.Autocomplete;

internal sealed class AutocompleteEngine(IServiceProvider services)
{
    private static readonly HashSet<string> WorkspaceIdentifierVerbs =
        new([CompletionCatalog.Get, CompletionCatalog.Delete, CompletionCatalog.Edit,
            CompletionCatalog.Use, CompletionCatalog.Copy, CompletionCatalog.Export],
            StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> EntityIdentifierVerbs =
        new([CompletionCatalog.Get, CompletionCatalog.Delete, CompletionCatalog.Edit,
            CompletionCatalog.Copy], StringComparer.OrdinalIgnoreCase);

    public static bool TryCompleteStatic(string query, out IReadOnlyList<string> completions)
    {
        ParsedQuery parsed = Parse(query);
        IReadOnlyList<string> tokens = parsed.Tokens;

        if (tokens.Count == 0 || (tokens.Count == 1 && !parsed.AtNewToken))
        {
            string partial = tokens.Count == 0 ? string.Empty : tokens[0];
            completions = Match(CompletionCatalog.TopLevelVerbs, partial);
            return true;
        }

        if (!CompletionCatalog.VerbNouns.TryGetValue(tokens[0], out string[]? nouns))
        {
            completions = [];
            return true;
        }

        if (!tokens[0].Equals(CompletionCatalog.Send, StringComparison.OrdinalIgnoreCase) &&
            ((tokens.Count == 1 && parsed.AtNewToken) || (tokens.Count == 2 && !parsed.AtNewToken)))
        {
            string partial = tokens.Count == 2 ? tokens[1] : string.Empty;
            completions = Match(nouns, partial);
            return true;
        }

        if (tokens[0].Equals(CompletionCatalog.Send, StringComparison.OrdinalIgnoreCase))
        {
            bool completingRequest =
                (tokens.Count == 1 && parsed.AtNewToken) ||
                (tokens.Count == 2 && !parsed.AtNewToken);
            completions = [];
            return !completingRequest;
        }

        bool completingIdentifier =
            (tokens.Count == 2 && parsed.AtNewToken) ||
            (tokens.Count == 3 && !parsed.AtNewToken);

        if (!completingIdentifier)
        {
            completions = [];
            return true;
        }

        string verb = tokens[0];
        string noun = tokens[1];
        bool needsDynamicState =
            CompletionCatalog.IsWorkspaceNoun(noun) && WorkspaceIdentifierVerbs.Contains(verb) ||
            (CompletionCatalog.IsRequestNoun(noun) ||
             CompletionCatalog.IsAuthNoun(noun) ||
             CompletionCatalog.IsSecretNoun(noun)) && EntityIdentifierVerbs.Contains(verb);

        completions = [];
        return !needsDynamicState;
    }

    public async Task<IReadOnlyList<string>> CompleteAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (TryCompleteStatic(query, out IReadOnlyList<string> staticCompletions))
        {
            return staticCompletions;
        }

        ParsedQuery parsed = Parse(query);
        IReadOnlyList<string> tokens = parsed.Tokens;
        string verb = tokens[0];
        string partial;

        if (verb.Equals(CompletionCatalog.Send, StringComparison.OrdinalIgnoreCase))
        {
            partial = tokens.Count == 2 ? tokens[1] : string.Empty;
            return await RequestCompletionsAsync(partial, cancellationToken);
        }

        string noun = tokens[1];
        partial = tokens.Count == 3 ? tokens[2] : string.Empty;

        if (CompletionCatalog.IsWorkspaceNoun(noun) && WorkspaceIdentifierVerbs.Contains(verb))
        {
            return await WorkspaceCompletionsAsync(partial, cancellationToken);
        }

        if (CompletionCatalog.IsRequestNoun(noun) && EntityIdentifierVerbs.Contains(verb))
        {
            return await RequestCompletionsAsync(partial, cancellationToken);
        }

        if (CompletionCatalog.IsAuthNoun(noun) && EntityIdentifierVerbs.Contains(verb))
        {
            return await AuthCompletionsAsync(partial, cancellationToken);
        }

        if (CompletionCatalog.IsSecretNoun(noun) && EntityIdentifierVerbs.Contains(verb))
        {
            return await SecretCompletionsAsync(partial, cancellationToken);
        }

        return [];
    }

    private async Task<IReadOnlyList<string>> WorkspaceCompletionsAsync(
        string partial,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<StraumrWorkspace> workspaces = await services
                .GetRequiredService<IStraumrWorkspaceService>()
                .ListAsync(cancellationToken);
            return Match(workspaces.SelectMany(workspace =>
                new[] { workspace.Id.ToString(), workspace.Name }), partial);
        }
        catch (StraumrException)
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<string>> RequestCompletionsAsync(
        string partial,
        CancellationToken cancellationToken)
    {
        IStraumrOptionsService optionsService = services.GetRequiredService<IStraumrOptionsService>();
        StraumrWorkspaceEntry? workspace = optionsService.Options.CurrentWorkspace;
        if (workspace is null)
        {
            return [];
        }

        try
        {
            IReadOnlyList<StraumrRequest> requests = await services
                .GetRequiredService<IStraumrRequestService>()
                .ListAsync(workspace, cancellationToken);
            return Match(requests.SelectMany(request =>
                new[] { request.Id.ToString(), request.Name }), partial);
        }
        catch (StraumrException)
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<string>> AuthCompletionsAsync(
        string partial,
        CancellationToken cancellationToken)
    {
        IStraumrOptionsService optionsService = services.GetRequiredService<IStraumrOptionsService>();
        StraumrWorkspaceEntry? workspace = optionsService.Options.CurrentWorkspace;
        if (workspace is null)
        {
            return [];
        }

        try
        {
            IReadOnlyList<StraumrAuth> auths = await services
                .GetRequiredService<IStraumrAuthService>()
                .ListAsync(workspace, cancellationToken);
            return Match(auths.SelectMany(auth =>
                new[] { auth.Id.ToString(), auth.Name }), partial);
        }
        catch (StraumrException)
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<string>> SecretCompletionsAsync(
        string partial,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<StraumrSecret> secrets = await services
                .GetRequiredService<IStraumrSecretService>()
                .ListAsync(cancellationToken);
            return Match(secrets.SelectMany(secret =>
                new[] { secret.Id.ToString(), secret.Name }), partial);
        }
        catch (StraumrException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> Match(IEnumerable<string> candidates, string partial) =>
        candidates
            .Where(candidate => candidate.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static ParsedQuery Parse(string query)
    {
        List<string> tokens = [];
        StringBuilder current = new();
        char quote = '\0';
        bool escaped = false;
        bool tokenStarted = false;

        foreach (char character in query)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                tokenStarted = true;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                tokenStarted = true;
                continue;
            }

            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }
                else
                {
                    current.Append(character);
                }

                tokenStarted = true;
                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                tokenStarted = true;
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (tokenStarted)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    tokenStarted = false;
                }

                continue;
            }

            current.Append(character);
            tokenStarted = true;
        }

        if (escaped)
        {
            current.Append('\\');
        }

        if (tokenStarted)
        {
            tokens.Add(current.ToString());
        }

        if (tokens.Count > 0 && CompletionCatalog.IsCliPrefix(tokens[0]) &&
            (tokens.Count > 1 || !tokenStarted))
        {
            tokens.RemoveAt(0);
        }

        return new ParsedQuery(tokens, AtNewToken: !tokenStarted);
    }

    private readonly record struct ParsedQuery(IReadOnlyList<string> Tokens, bool AtNewToken);
}
