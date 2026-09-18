using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Straumr.Core.Configuration;
using Straumr.Core.Helpers;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Screens.Secret;

internal sealed record SecretRewrite(int References, int Resources, IReadOnlyList<string> Problems);

internal static class SecretReferenceRewrite
{
    public static async Task<SecretRewrite> ApplyAsync(
        IReadOnlyList<StraumrWorkspaceEntry> entries,
        IReadOnlyList<SecretUsage> usages,
        string oldName,
        string newName,
        IStraumrRequestService requests,
        IStraumrAuthService auths,
        CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        int references = 0;
        int resources = 0;
        foreach (var resource in usages.GroupBy(usage => (usage.WorkspaceId, usage.ResourceId)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            SecretUsage usage = resource.First();
            if (entries.FirstOrDefault(entry => entry.Id == usage.WorkspaceId) is not { } entry ||
                !File.Exists(entry.Path))
            {
                problems.Add($"{usage.Workspace} is no longer registered");
                continue;
            }

            try
            {
                int rewritten = usage.Kind == "Request"
                    ? await RewriteRequestAsync(entry, usage.ResourceId, oldName, newName, requests, cancellationToken)
                    : await RewriteAuthAsync(entry, usage.ResourceId, oldName, newName, auths, cancellationToken);
                if (rewritten == 0)
                    continue;
                references += rewritten;
                resources++;
            }
            catch (Exception exception) when (SecretScreen.IsRecoverable(exception))
            {
                problems.Add($"{usage.Workspace} · {usage.Resource}: {exception.Message}");
            }
        }

        return new SecretRewrite(references, resources, problems);
    }

    private static async Task<int> RewriteRequestAsync(StraumrWorkspaceEntry entry, Guid id, string oldName,
        string newName, IStraumrRequestService requests, CancellationToken cancellationToken)
    {
        StraumrRequest request = await requests.GetAsync(entry, id, updateLastAccessed: false, cancellationToken);
        (StraumrRequest? rewritten, int count) =
            Rewritten(request, id, StraumrJsonContext.Default.StraumrRequest, oldName, newName);
        if (rewritten is null)
            return 0;
        await requests.SaveAsync(entry, rewritten, cancellationToken);
        return count;
    }

    private static async Task<int> RewriteAuthAsync(StraumrWorkspaceEntry entry, Guid id, string oldName,
        string newName, IStraumrAuthService auths, CancellationToken cancellationToken)
    {
        StraumrAuth auth = await auths.GetAsync(entry, id, updateLastAccessed: false, cancellationToken);
        (StraumrAuth? rewritten, int count) =
            Rewritten(auth, id, StraumrJsonContext.Default.StraumrAuth, oldName, newName);
        if (rewritten is null)
            return 0;
        await auths.SaveAsync(entry, rewritten, cancellationToken);
        return count;
    }

    private static (T? Rewritten, int Count) Rewritten<T>(T model, Guid id, JsonTypeInfo<T> typeInfo,
        string oldName, string newName) where T : StraumrModelBase
    {
        if (model.Id != id)
            throw new InvalidDataException("the stored ID no longer matches its workspace entry");
        if (JsonSerializer.SerializeToNode(model, typeInfo) is not { } document)
            return (null, 0);

        int count = 0;
        JsonNode? rewritten = Rewrite(document, oldName, newName, ref count);
        if (count == 0 || rewritten is null)
            return (null, 0);

        T? deserialized = rewritten.Deserialize(typeInfo);
        if (deserialized is null || deserialized.Id != id)
            throw new InvalidDataException("the rewritten resource could not be read back");
        return (deserialized, count);
    }

    private static JsonNode? Rewrite(JsonNode? node, string oldName, string newName, ref int count)
    {
        switch (node)
        {
            case JsonObject source:
                var target = new JsonObject();
                foreach ((string name, JsonNode? value) in source)
                    target[name] = Resolved(name, value)
                        ? Rewrite(value, oldName, newName, ref count)
                        : value?.DeepClone();
                return target;
            case JsonArray source:
                var array = new JsonArray();
                foreach (JsonNode? value in source)
                    array.Add(Rewrite(value, oldName, newName, ref count));
                return array;
            case JsonValue value when value.TryGetValue(out string? text):
                return JsonValue.Create(Replace(text, oldName, newName, ref count));
            default:
                return node?.DeepClone();
        }
    }

    private static bool Resolved(string name, JsonNode? value) =>
        name != "CachedValue" && (name != "Token" || value?.GetValueKind() != JsonValueKind.Object);

    private static string Replace(string text, string oldName, string newName, ref int count)
    {
        var builder = new StringBuilder();
        int index = 0;
        foreach (Match match in SecretHelpers.SecretPattern.Matches(text))
        {
            if (!match.Groups["name"].Value.Trim().Equals(oldName, StringComparison.OrdinalIgnoreCase))
                continue;
            builder.Append(text, index, match.Index - index).Append("{{secret:").Append(newName).Append("}}");
            index = match.Index + match.Length;
            count++;
        }

        return index == 0 ? text : builder.Append(text, index, text.Length - index).ToString();
    }
}
