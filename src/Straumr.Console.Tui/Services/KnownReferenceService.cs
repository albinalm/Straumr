using System.Text.Json;
using Straumr.Core.Configuration;
using Straumr.Core.Helpers;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Services;

internal sealed class KnownReferenceService
{
    private readonly Dictionary<string, List<ReferenceUsageModel>> _secrets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ReferenceUsageModel>> _variables = new(StringComparer.OrdinalIgnoreCase);
    public int ScannedWorkspaces { get; private set; }
    public int UnreadableWorkspaces { get; private set; }
    public int UnreadableResources { get; private set; }

    public string? Notice => UnreadableWorkspaces + UnreadableResources == 0 ? null :
        $"Reference scan incomplete: {UnreadableWorkspaces} workspace(s) and {UnreadableResources} resource(s) could not be read.";

    public IReadOnlyList<ReferenceUsageModel> ForSecret(string name) =>
        _secrets.TryGetValue(name, out List<ReferenceUsageModel>? references) ? references : [];

    public IReadOnlyList<ReferenceUsageModel> ForVariable(Guid workspace, string name) =>
        _variables.TryGetValue(VariableKey(workspace, name), out List<ReferenceUsageModel>? references) ? references : [];

    public static async Task<KnownReferenceService> LoadAsync(
        IEnumerable<StraumrWorkspaceEntry> entries, IStraumrWorkspaceService workspaces,
        IStraumrRequestService requests, IStraumrAuthService auths, CancellationToken cancellationToken)
    {
        var index = new KnownReferenceService();
        foreach (StraumrWorkspaceEntry entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(entry.Path))
            {
                continue;
            }

            StraumrWorkspace workspace;
            try
            {
                workspace = await workspaces.GetAsync(entry.Id, false, cancellationToken);
                if (workspace.Id != entry.Id || workspace.Requests is null || workspace.Auths is null)
                {
                    index.UnreadableWorkspaces++;
                    continue;
                }
            }
            catch (Exception exception) when (SecretScreen.IsRecoverable(exception))
            {
                index.UnreadableWorkspaces++;
                continue;
            }

            index.ScannedWorkspaces++;
            foreach (Guid id in workspace.Requests)
            {
                try
                {
                    StraumrRequest request = await requests.GetAsync(entry, id, false, cancellationToken);
                    if (request.Id != id)
                    {
                        index.UnreadableResources++;
                        continue;
                    }
                    JsonElement document = JsonSerializer.SerializeToElement(request, StraumrJsonContext.Default.StraumrRequest);
                    foreach (JsonProperty field in document.EnumerateObject().Where(field =>
                                 field.Name is "Uri" or "Headers" or "Params" or "Bodies"))
                    {
                        index.Scan(field.Value, Label(field.Name), workspace, request, "Request", null);
                    }
                }
                catch (Exception exception) when (SecretScreen.IsRecoverable(exception))
                {
                    index.UnreadableResources++;
                }
            }

            foreach (Guid id in workspace.Auths)
            {
                try
                {
                    StraumrAuth auth = await auths.GetAsync(entry, id, false, cancellationToken);
                    if (auth.Id != id || auth.Config is null)
                    {
                        index.UnreadableResources++;
                        continue;
                    }
                    JsonElement document = JsonSerializer.SerializeToElement(auth, StraumrJsonContext.Default.StraumrAuth);
                    foreach (JsonProperty field in document.GetProperty("Config").EnumerateObject())
                    {
                        if (field.Name == "CachedValue" || field.Name == "Token" && field.Value.ValueKind == JsonValueKind.Object)
                        {
                            continue;
                        }

                        index.Scan(field.Value, Label(field.Name), workspace, auth, "Auth",
                            field.Name == CustomAuthConfig.TemplateField ? CustomAuthConfig.ValueName : null);
                    }
                }
                catch (Exception exception) when (SecretScreen.IsRecoverable(exception))
                {
                    index.UnreadableResources++;
                }
            }
        }

        foreach (List<ReferenceUsageModel> references in index._secrets.Values.Concat(index._variables.Values))
        {
            references.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(
                $"{left.Workspace}\0{left.Resource}\0{left.Field}", $"{right.Workspace}\0{right.Resource}\0{right.Field}"));
        }

        return index;
    }

    private static string VariableKey(Guid workspace, string name) => $"{workspace}\0{name}";

    private void Scan(JsonElement element, string field, StraumrWorkspace workspace, StraumrModelBase resource,
        string kind, string? reservedName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                Scan(property.Value, $"{field} · {property.Name}", workspace, resource, kind, reservedName);
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            foreach (string token in VariableHelpers.ReferencePattern.Matches(element.GetString()!)
                         .Select(match => match.Groups["name"].Value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                bool isSecret = VariableHelpers.IsSecretName(token);
                string name = isSecret ? VariableHelpers.StripSecretPrefix(token) : token;
                if (!isSecret && name.Equals(reservedName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Dictionary<string, List<ReferenceUsageModel>> target = isSecret ? _secrets : _variables;
                string key = isSecret ? name : VariableKey(workspace.Id, name);
                if (!target.TryGetValue(key, out List<ReferenceUsageModel>? references))
                {
                    target[key] = references = [];
                }

                references.Add(new ReferenceUsageModel(workspace.Id, resource.Id, workspace.Name, resource.Name, kind, field));
            }
        }
    }

    private static string Label(string name) => name switch
    {
        "Uri" or "Url" => "URL",
        "Params" => "Parameters",
        "Bodies" => "Body",
        "ClientId" => "Client ID",
        "ClientSecret" => "Client secret",
        "TokenUrl" => "Token URL",
        "AuthorizationUrl" => "Authorization URL",
        "RedirectUri" => "Redirect URI",
        "CodeChallengeMethod" => "Challenge method",
        "ExtractionExpression" => "Extraction expression",
        "ApplyHeaderName" => "Header name",
        "ApplyHeaderTemplate" => "Header template",
        _ => name
    };
}
