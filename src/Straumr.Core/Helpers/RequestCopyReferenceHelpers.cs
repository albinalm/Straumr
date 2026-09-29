using System.Text.Json;
using System.Text.Json.Nodes;
using Straumr.Core.Configuration;

namespace Straumr.Core.Helpers;

internal static class RequestCopyReferenceHelpers
{
    public static IEnumerable<string> Names(StraumrRequest request, StraumrAuth? auth) =>
        RequestValues(request).SelectMany(value => Names(value, null))
            .Concat(AuthFields(auth).SelectMany(field => Scan(field.Value,
                field.Key == CustomAuthConfig.TemplateField ? CustomAuthConfig.ValueName : null)))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    public static StraumrRequest Request(RequestCopyPlanModel plan)
    {
        StraumrRequest copy = plan.Original.CopyAs(plan.Copy.Name);
        copy.Id = plan.Copy.Id;
        if (!plan.CarryDependencies)
        {
            return copy;
        }
        Dictionary<string, string> names = VariableNames(plan);
        copy.Uri = Replace(copy.Uri, names, null);
        copy.Headers = copy.Headers.ToDictionary(pair => pair.Key, pair => Replace(pair.Value, names, null), copy.Headers.Comparer);
        copy.Params = copy.Params.ToDictionary(pair => pair.Key, pair => Replace(pair.Value, names, null), copy.Params.Comparer);
        copy.Bodies = copy.Bodies.ToDictionary(pair => pair.Key, pair => Replace(pair.Value, names, null));
        if (plan.Auth is { } auth)
        {
            copy.AuthId = auth.TargetId;
        }
        return copy;
    }
    public static StraumrAuth Auth(RequestCopyPlanModel plan, DependencyCopyModel dependency)
    {
        StraumrAuth copy = ((StraumrAuth)dependency.Original).CopyAs(dependency.Copy.Name);
        copy.Id = dependency.TargetId;
        JsonObject document = JsonSerializer.SerializeToNode(copy, StraumrJsonContext.Default.StraumrAuth)!.AsObject();
        JsonObject config = document[nameof(StraumrAuth.Config)]!.AsObject();
        Dictionary<string, string> names = VariableNames(plan);
        foreach (KeyValuePair<string, JsonNode?> field in AuthFields(copy))
        {
            config[field.Key] = Rewrite(field.Value, names,
                field.Key == CustomAuthConfig.TemplateField ? CustomAuthConfig.ValueName : null);
        }
        return document.Deserialize(StraumrJsonContext.Default.StraumrAuth)!;
    }
    private static Dictionary<string, string> VariableNames(RequestCopyPlanModel plan) =>
        plan.Variables.ToDictionary(variable => variable.Original.Name,
            variable => variable.Action == DependencyCopyAction.UseExisting ? variable.Existing!.Name : variable.Copy.Name,
            StringComparer.OrdinalIgnoreCase);
    private static IEnumerable<string> RequestValues(StraumrRequest request) =>
        new[] { request.Uri }.Concat(request.Headers.Values).Concat(request.Params.Values).Concat(request.Bodies.Values);
    private static IEnumerable<KeyValuePair<string, JsonNode?>> AuthFields(StraumrAuth? auth)
    {
        if (auth is null)
        {
            return [];
        }
        JsonObject config = JsonSerializer.SerializeToNode(auth, StraumrJsonContext.Default.StraumrAuth)!
            [nameof(StraumrAuth.Config)]!.AsObject();
        return config.Where(field => field.Key != "CachedValue" && (field.Key != "Token" || field.Value is not JsonObject));
    }
    private static IEnumerable<string> Scan(JsonNode? node, string? reserved)
    {
        if (node is JsonObject document)
        {
            return document.SelectMany(field => Scan(field.Value, reserved));
        }
        if (node is JsonArray array)
        {
            return array.SelectMany(value => Scan(value, reserved));
        }
        return node is JsonValue value && value.TryGetValue(out string? text) ? Names(text, reserved) : [];
    }
    private static IEnumerable<string> Names(string text, string? reserved) =>
        VariableHelpers.ReferencePattern.Matches(text).Select(match => match.Groups["name"].Value.Trim())
            .Where(name => !VariableHelpers.IsSecretName(name) && !name.Equals(reserved, StringComparison.OrdinalIgnoreCase));
    private static JsonNode? Rewrite(JsonNode? node, IReadOnlyDictionary<string, string> names, string? reserved) => node switch
    {
        JsonObject document => new JsonObject(document.Select(field =>
            new KeyValuePair<string, JsonNode?>(field.Key, Rewrite(field.Value, names, reserved)))),
        JsonArray array => new JsonArray(array.Select(value => Rewrite(value, names, reserved)).ToArray()),
        JsonValue value when value.TryGetValue(out string? text) => JsonValue.Create(Replace(text, names, reserved)),
        _ => node?.DeepClone()
    };
    private static string Replace(string text, IReadOnlyDictionary<string, string> names, string? reserved) =>
        VariableHelpers.ReferencePattern.Replace(text, match =>
        {
            string name = match.Groups["name"].Value.Trim();
            return !VariableHelpers.IsSecretName(name) && !name.Equals(reserved, StringComparison.OrdinalIgnoreCase) &&
                   names.TryGetValue(name, out string? renamed) && !name.Equals(renamed, StringComparison.Ordinal)
                ? "{{" + renamed + "}}" : match.Value;
        });
}
