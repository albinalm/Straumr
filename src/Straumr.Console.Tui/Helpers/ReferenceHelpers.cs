using System.Text.Json;
using Straumr.Core.Configuration;
using Straumr.Core.Helpers;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Helpers;

public static class ReferenceHelpers
{
    public static async Task<IReadOnlyList<ReferenceModel>> ResolveAsync(
        IStraumrSecretService secrets,
        IStraumrVariableService variables,
        StraumrWorkspaceEntry workspace,
        StraumrRequest? request,
        StraumrAuth? auth,
        Func<Exception, bool> isRecoverable,
        CancellationToken cancellationToken)
    {
        List<ReferenceModel> references = new();
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach ((string name, bool isSecret) in Tokens(request, auth))
        {
            if (!seen.Add($"{isSecret}:{name}"))
            {
                continue;
            }

            try
            {
                if (isSecret)
                {
                    await secrets.GetAsync(name, false, cancellationToken);
                }
                else
                {
                    await variables.GetAsync(workspace, name, false, cancellationToken);
                }

                references.Add(new ReferenceModel(name, isSecret, true));
            }
            catch (Exception exception) when (isRecoverable(exception))
            {
                references.Add(new ReferenceModel(name, isSecret, false));
            }
        }

        return references;
    }

    private static IEnumerable<(string Name, bool IsSecret)> Tokens(StraumrRequest? request, StraumrAuth? auth)
    {
        if (request is not null)
        {
            JsonElement document = JsonSerializer.SerializeToElement(request, StraumrJsonContext.Default.StraumrRequest);
            foreach (JsonProperty field in document.EnumerateObject().Where(field =>
                         field.Name is "Uri" or "Headers" or "Params" or "Bodies"))
            {
                foreach ((string Name, bool IsSecret) token in Scan(field.Value, null))
                {
                    yield return token;
                }
            }
        }

        if (auth?.Config is null)
        {
            yield break;
        }

        JsonElement config = JsonSerializer
            .SerializeToElement(auth, StraumrJsonContext.Default.StraumrAuth).GetProperty("Config");
        foreach (JsonProperty field in config.EnumerateObject())
        {
            if (field.Name == "CachedValue" || field.Name == "Token" && field.Value.ValueKind == JsonValueKind.Object)
            {
                continue;
            }

            string? reserved = field.Name == CustomAuthConfig.TemplateField ? CustomAuthConfig.ValueName : null;
            foreach ((string Name, bool IsSecret) token in Scan(field.Value, reserved))
            {
                yield return token;
            }
        }
    }

    private static IEnumerable<(string Name, bool IsSecret)> Scan(JsonElement element, string? reservedName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    foreach ((string Name, bool IsSecret) token in Scan(property.Value, reservedName))
                    {
                        yield return token;
                    }
                }

                break;
            case JsonValueKind.String:
                foreach (string token in VariableHelpers.ReferencePattern.Matches(element.GetString()!)
                             .Select(match => match.Groups["name"].Value.Trim()))
                {
                    bool isSecret = VariableHelpers.IsSecretName(token);
                    string name = isSecret ? VariableHelpers.StripSecretPrefix(token) : token;
                    if (isSecret || !name.Equals(reservedName, StringComparison.OrdinalIgnoreCase))
                    {
                        yield return (name, isSecret);
                    }
                }

                break;
        }
    }
}
