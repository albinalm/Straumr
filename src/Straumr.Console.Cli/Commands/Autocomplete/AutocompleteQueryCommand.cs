using System.ComponentModel;
using Spectre.Console.Cli;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Cli.Commands.Autocomplete;
// ReSharper disable UnusedAutoPropertyAccessor.Global

public class AutocompleteQueryCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService,
    IStraumrAuthService authService,
    IStraumrSecretService secretService)
    : AsyncCommand<AutocompleteQueryCommand.Settings>
{
    // Top-level verbs registered in Program.cs
    private static readonly string[] TopLevelVerbs =
    [
        "list", "create", "delete", "edit", "get",
        "use", "copy", "import", "export",
        "config", "autocomplete", "send"
    ];

    // Nouns available under each verb (full names only, no aliases)
    private static readonly Dictionary<string, string[]> VerbNouns = new()
    {
        ["list"]       = ["workspace", "request", "auth", "secret"],
        ["create"]     = ["workspace", "request", "auth", "secret"],
        ["delete"]     = ["workspace", "request", "auth", "secret"],
        ["edit"]       = ["workspace", "request", "auth", "secret"],
        ["get"]        = ["workspace", "request", "auth", "secret"],
        ["use"]        = ["workspace"],
        ["copy"]       = ["workspace"],
        ["import"]     = ["workspace"],
        ["export"]     = ["workspace"],
        ["config"]     = ["workspace-path"],
        ["autocomplete"] = ["install"],
        ["send"]       = [],
    };

    // Which verbs offer workspace-name completion at position 3
    private static readonly HashSet<string> WorkspaceIdentifierVerbs =
        ["get", "delete", "edit", "use", "copy", "export"];

    // Which verbs offer request-name completion at position 3
    private static readonly HashSet<string> RequestIdentifierVerbs =
        ["get", "delete", "edit"];

    // Which verbs offer auth-name completion at position 3
    private static readonly HashSet<string> AuthIdentifierVerbs =
        ["get", "delete", "edit"];

    // Which verbs offer secret-name completion at position 3
    private static readonly HashSet<string> SecretIdentifierVerbs =
        ["get", "delete", "edit"];

    private static readonly HashSet<string> WorkspaceNouns = ["workspace", "ws"];
    private static readonly HashSet<string> RequestNouns   = ["request", "rq"];
    private static readonly HashSet<string> AuthNouns      = ["auth", "au"];
    private static readonly HashSet<string> SecretNouns    = ["secret", "sc"];

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellationToken)
    {
        string[] parts = settings.Query.Split(' ');
        bool trailingSpace = parts[^1] == string.Empty;
        string[] tokens = parts.Where(p => p != string.Empty).ToArray();

        var completions = new CompletionResult();

        // Position 1: complete top-level verb
        if (tokens.Length == 0 || (tokens.Length == 1 && !trailingSpace))
        {
            string partial = tokens.Length == 0 ? string.Empty : tokens[0];
            completions.Add(TopLevelVerbs.Where(v => v.StartsWith(partial, StringComparison.OrdinalIgnoreCase)));
        }

        else if (VerbNouns.TryGetValue(tokens[0], out string[]? nouns))
        {
            string verb = tokens[0];

            // 'send' takes a request identifier directly at position 2 (no noun)
            if (verb == "send")
            {
                if ((tokens.Length == 1 && trailingSpace) || (tokens.Length == 2 && !trailingSpace))
                {
                    string partial = tokens.Length == 2 ? tokens[1] : string.Empty;
                    if (optionsService.Options.CurrentWorkspace is not null)
                    {
                        await AddRequestCompletionsAsync(completions, partial);
                    }
                }
            }

            // Position 2: complete noun for the verb
            else if ((tokens.Length == 1 && trailingSpace) || (tokens.Length == 2 && !trailingSpace))
            {
                string partial = tokens.Length == 2 ? tokens[1] : string.Empty;
                completions.Add(nouns.Where(n => n.StartsWith(partial, StringComparison.OrdinalIgnoreCase)));
            }

            // Position 3: complete identifier for verb + noun
            else if ((tokens.Length == 2 && trailingSpace) || (tokens.Length == 3 && !trailingSpace))
            {
                string noun    = tokens[1];
                string partial = tokens.Length == 3 ? tokens[2] : string.Empty;

                if (WorkspaceNouns.Contains(noun) && WorkspaceIdentifierVerbs.Contains(verb))
                {
                    await AddWorkspaceCompletionsAsync(completions, partial);
                }
                else if (RequestNouns.Contains(noun) && RequestIdentifierVerbs.Contains(verb)
                         && optionsService.Options.CurrentWorkspace is not null)
                {
                    await AddRequestCompletionsAsync(completions, partial);
                }
                else if (AuthNouns.Contains(noun) && AuthIdentifierVerbs.Contains(verb)
                         && optionsService.Options.CurrentWorkspace is not null)
                {
                    await AddAuthCompletionsAsync(completions, partial);
                }
                else if (SecretNouns.Contains(noun) && SecretIdentifierVerbs.Contains(verb))
                {
                    await AddSecretCompletionsAsync(completions, partial);
                }
            }
        }

        completions.Flush();
        return 0;
    }

    private async Task AddWorkspaceCompletionsAsync(CompletionResult completions, string partial)
    {
        foreach (StraumrWorkspaceEntry entry in optionsService.Options.Workspaces)
        {
            if (entry.Id.ToString().StartsWith(partial, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(entry.Id.ToString());
            }

            try
            {
                StraumrWorkspace workspace = await workspaceService.PeekWorkspace(entry.Path);
                if (workspace.Name.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(workspace.Name);
                }
            }
            catch (StraumrException) { }
        }
    }

    private async Task AddRequestCompletionsAsync(CompletionResult completions, string partial)
    {
        StraumrWorkspaceEntry workspaceEntry = optionsService.Options.CurrentWorkspace!;

        StraumrWorkspace workspace;
        try
        {
            workspace = await workspaceService.PeekWorkspace(workspaceEntry.Path);
        }
        catch (StraumrException)
        {
            return;
        }

        foreach (Guid id in workspace.Requests)
        {
            if (id.ToString().StartsWith(partial, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(id.ToString());
            }

            try
            {
                StraumrRequest request = await requestService.PeekByIdAsync(id);
                if (request.Name.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(request.Name);
                }
            }
            catch (StraumrException) { }
        }
    }

    private async Task AddAuthCompletionsAsync(CompletionResult completions, string partial)
    {
        StraumrWorkspaceEntry workspaceEntry = optionsService.Options.CurrentWorkspace!;

        StraumrWorkspace workspace;
        try
        {
            workspace = await workspaceService.PeekWorkspace(workspaceEntry.Path);
        }
        catch (StraumrException)
        {
            return;
        }

        foreach (Guid id in workspace.Auths)
        {
            if (id.ToString().StartsWith(partial, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(id.ToString());
            }

            try
            {
                StraumrAuth auth = await authService.PeekByIdAsync(id);
                if (auth.Name.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(auth.Name);
                }
            }
            catch (StraumrException) { }
        }
    }

    private async Task AddSecretCompletionsAsync(CompletionResult completions, string partial)
    {
        foreach (StraumrSecretEntry entry in optionsService.Options.Secrets.Where(e => File.Exists(e.Path)))
        {
            if (entry.Id.ToString().StartsWith(partial, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(entry.Id.ToString());
            }

            try
            {
                StraumrSecret secret = await secretService.PeekByIdAsync(entry.Id);
                if (secret.Name.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(secret.Name);
                }
            }
            catch (StraumrException) { }
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Query>")]
        [Description("Current command line to complete")]
        public required string Query { get; set; }
    }
}

internal sealed class CompletionResult
{
    private readonly List<string> _candidates = [];

    public void Add(string candidate)
    {
        _candidates.Add(candidate);
    }

    public void Add(IEnumerable<string> candidates)
    {
        _candidates.AddRange(candidates);
    }

    public void Flush()
    {
        foreach (string candidate in _candidates)
            System.Console.WriteLine(candidate);
    }
}
