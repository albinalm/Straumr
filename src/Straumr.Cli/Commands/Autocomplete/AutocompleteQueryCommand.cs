using System.ComponentModel;
using Spectre.Console.Cli;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Autocomplete;

public class AutocompleteQueryCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService,
    IStraumrAuthService authService,
    IStraumrSecretService secretService)
    : AsyncCommand<AutocompleteQueryCommand.Settings>
{
    private static readonly string[] Branches = ["workspace", "request", "auth", "secret", "config"];

    private static readonly Dictionary<string, string[]> BranchCommands = new()
    {
        ["workspace"] = ["create", "use", "import", "list", "delete", "export", "edit", "get"],
        ["request"] = ["create", "send", "edit", "list", "delete", "get"],
        ["auth"] = ["create", "edit", "list", "delete", "get"],
        ["secret"] = ["create", "edit", "list", "delete", "get"],
        ["config"] = ["workspace-path"]
    };

    private static readonly string[] WorkspaceIdentifierCommands = ["use", "delete", "export", "edit", "get"];
    private static readonly string[] RequestIdentifierCommands = ["send", "edit", "delete", "get"];
    private static readonly string[] AuthIdentifierCommands = ["edit", "delete", "get"];
    private static readonly string[] SecretIdentifierCommands = ["edit", "delete", "get"];

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellationToken)
    {
        string[] parts = settings.Query.Split(' ');
        bool trailingSpace = parts[^1] == string.Empty;
        string[] tokens = parts.Where(p => p != string.Empty).ToArray();

        var completions = new CompletionResult();

        if (tokens.Length == 0 || (tokens.Length == 1 && !trailingSpace))
        {
            string partial = tokens.Length == 0 ? string.Empty : tokens[0];
            completions.Add(Branches.Where(b => b.StartsWith(partial, StringComparison.OrdinalIgnoreCase)));
        }

        else if (BranchCommands.TryGetValue(tokens[0], out string[]? commands) &&
                 (tokens.Length == 1 || (tokens.Length == 2 && !trailingSpace)))
        {
            string partial = tokens.Length == 2 ? tokens[1] : string.Empty;
            completions.Add(commands.Where(c => c.StartsWith(partial, StringComparison.OrdinalIgnoreCase)));
        }

        else if (tokens.Length == 3 && !trailingSpace &&
                 tokens[0] == "workspace" &&
                 WorkspaceIdentifierCommands.Contains(tokens[1]))
        {
            await AddWorkspaceCompletionsAsync(completions, tokens[2]);
        }

        else if (tokens.Length == 3 && !trailingSpace &&
                 tokens[0] == "request" &&
                 RequestIdentifierCommands.Contains(tokens[1]) &&
                 optionsService.Options.CurrentWorkspace is not null)
        {
            await AddRequestCompletionsAsync(completions, tokens[2]);
        }

        else if (tokens.Length == 3 && !trailingSpace &&
                 tokens[0] == "auth" &&
                 AuthIdentifierCommands.Contains(tokens[1]) &&
                 optionsService.Options.CurrentWorkspace is not null)
        {
            await AddAuthCompletionsAsync(completions, tokens[2]);
        }
        else if (tokens.Length == 3 && !trailingSpace &&
                 tokens[0] == "secret" &&
                 SecretIdentifierCommands.Contains(tokens[1]))
        {
            await AddSecretCompletionsAsync(completions, tokens[2]);
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
                StraumrWorkspace workspace = await workspaceService.GetWorkspace(entry.Path);
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
            workspace = await workspaceService.GetWorkspace(workspaceEntry.Path);
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
                StraumrRequest request = await requestService.GetAsync(id.ToString());
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
            workspace = await workspaceService.GetWorkspace(workspaceEntry.Path);
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
                StraumrAuth auth = await authService.GetAsync(id.ToString());
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
        foreach (StraumrSecretEntry entry in optionsService.Options.Secrets.Where(entry => File.Exists(entry.Path)))
        {
            if (entry.Id.ToString().StartsWith(partial, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(entry.Id.ToString());
            }

            try
            {
                StraumrSecret secret = await secretService.GetAsync(entry.Id.ToString());
                if (secret.Name.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(secret.Name);
                }
            }
            catch (StraumrException)
            {
            }
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
