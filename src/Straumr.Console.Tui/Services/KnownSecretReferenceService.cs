using System.Text.Json;
using Straumr.Core.Configuration;
using Straumr.Core.Helpers;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Services;

internal sealed class KnownSecretReferenceService
{
    private readonly Dictionary<string, List<SecretUsageModel>> _byName = new(StringComparer.OrdinalIgnoreCase);
    public int ScannedWorkspaces { get; private set; }
    public int UnreadableWorkspaces { get; private set; }
    public int UnreadableResources { get; private set; }

    public string? Notice => UnreadableWorkspaces + UnreadableResources == 0 ? null :
        $"Reference scan incomplete: {UnreadableWorkspaces} workspace(s) and {UnreadableResources} resource(s) could not be read.";

    public IReadOnlyList<SecretUsageModel> For(string name) =>
        _byName.TryGetValue(name, out List<SecretUsageModel>? references) ? references : [];

    public static async Task<KnownSecretReferenceService> LoadAsync(
        IEnumerable<StraumrWorkspaceEntry> entries, IStraumrWorkspaceService workspaces,
        IStraumrRequestService requests, IStraumrAuthService auths, CancellationToken cancellationToken)
    {
        var index = new KnownSecretReferenceService();
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
                        index.Scan(field.Value, Label(field.Name), workspace, request, "Request");
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

                        index.Scan(field.Value, Label(field.Name), workspace, auth, "Auth");
                    }
                }
                catch (Exception exception) when (SecretScreen.IsRecoverable(exception))
                {
                    index.UnreadableResources++;
                }
            }
        }

        foreach (List<SecretUsageModel> references in index._byName.Values)
        {
            references.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(
                $"{left.Workspace}\0{left.Resource}\0{left.Field}", $"{right.Workspace}\0{right.Resource}\0{right.Field}"));
        }

        return index;
    }

    private void Scan(JsonElement element, string field, StraumrWorkspace workspace, StraumrModelBase resource, string kind)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                Scan(property.Value, $"{field} · {property.Name}", workspace, resource, kind);
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            foreach (string name in SecretHelpers.SecretPattern.Matches(element.GetString()!)
                         .Select(match => match.Groups["name"].Value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!_byName.TryGetValue(name, out List<SecretUsageModel>? references))
                {
                    _byName[name] = references = [];
                }

                references.Add(new SecretUsageModel(workspace.Id, resource.Id, workspace.Name, resource.Name, kind, field));
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
