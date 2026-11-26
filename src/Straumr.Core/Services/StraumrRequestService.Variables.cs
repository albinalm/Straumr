using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Straumr.Core.Configuration;
using Straumr.Core.Exceptions;

namespace Straumr.Core.Services;

public partial class StraumrRequestService
{
    private readonly AsyncLocal<RequestResolutionContext?> _resolution = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _variableLocks = new(StringComparer.OrdinalIgnoreCase);
    public Task<StraumrVariable> RefreshVariableAsync(StraumrWorkspaceEntry workspace, Guid id, CancellationToken cancellationToken = default) =>
        InResolutionAsync(true, null, async () =>
        {
            StraumrVariable variable = await variableService.GetAsync(workspace, id, false, cancellationToken);
            if (variable.Source is null)
            {
                throw new StraumrException("Only request-backed variables can be refreshed", StraumrError.InvalidEntry);
            }
            await ResolveVariableValueAsync(workspace, variable, true, [], cancellationToken);
            return await variableService.GetAsync(workspace, id, false, cancellationToken);
        });
    private async Task<T> InResolutionAsync<T>(bool allowFetch, SendOptions? options, Func<Task<T>> action)
    {
        RequestResolutionContext? previous = _resolution.Value;
        _resolution.Value = new RequestResolutionContext(allowFetch, options);
        try
        {
            return await action();
        }
        finally
        {
            _resolution.Value = previous;
        }
    }
    private void EnterResolution(string key, string label)
    {
        List<(string Key, string Label)> chain = _resolution.Value!.Chain;
        if (chain.Any(entry => entry.Key == key))
        {
            throw new StraumrException($"Request dependency cycle: {string.Join(" -> ", chain.Select(entry => entry.Label).Append(label))}",
                StraumrError.InvalidEntry);
        }
        chain.Add((key, label));
    }
    private async Task<StraumrResponse> SendTrackedAsync(
        StraumrWorkspaceEntry workspace, StraumrRequest request, bool capture, CancellationToken cancellationToken)
    {
        EnterResolution($"request:{request.Id}", $"request '{request.Name}'");
        try
        {
            if (capture)
            {
                await ValidateCapturesAsync(workspace, request, cancellationToken);
            }
            return await SendCoreAsync(workspace, request, capture, cancellationToken);
        }
        finally
        {
            _resolution.Value!.Chain.RemoveAt(_resolution.Value.Chain.Count - 1);
        }
    }
    private async Task<string> ResolveVariableValueAsync(StraumrWorkspaceEntry workspace, StraumrVariable variable,
        bool force, List<string> warnings, CancellationToken cancellationToken)
    {
        if (variable.Source is null)
        {
            return variable.Value;
        }
        EnterResolution($"variable:{variable.Id}", $"variable '{variable.Name}'");
        RequestResolutionContext context = _resolution.Value!;
        SemaphoreSlim gate = _variableLocks.GetOrAdd(Path.GetFullPath(workspace.Path), _ => new SemaphoreSlim(1, 1));
        bool ownsLock = false;
        try
        {
            if (context.AllowFetch && !context.HasVariableLock)
            {
                await gate.WaitAsync(cancellationToken);
                ownsLock = true;
                context.HasVariableLock = true;
            }
            variable = await variableService.GetAsync(workspace, variable.Id, false, cancellationToken);
            if (variable.Source is not { } source)
            {
                return variable.Value;
            }
            ResponseValueExtractor.Validate(source.ExtractionSource, source.Expression);
            if (source.TimeToLiveSeconds is <= 0)
            {
                throw new StraumrException("Variable TTL must be a positive number of seconds", StraumrError.InvalidEntry);
            }
            string key = await VariableFingerprintAsync(workspace, variable, cancellationToken);
            if (!force && variable.CachedValue is { Length: > 0 } cached && variable.CacheKey == key && !CacheExpired(variable))
            {
                return cached;
            }
            if (!context.AllowFetch)
            {
                warnings.Add($"Request-backed variable '{variable.Name}' has no valid cached value; dry-run will not fetch it.");
                return "{{" + variable.Name + "}}";
            }
            string configuration = await VariableFingerprintAsync(workspace, variable, cancellationToken, false);
            StraumrRequest request = await GetAsync(workspace, source.RequestId, false, cancellationToken);
            context.Notices.Add($"Fetching variable '{variable.Name}' from request '{request.Name}' ({request.Id}).");
            StraumrResponse response = await SendTrackedAsync(workspace, request, false, cancellationToken);
            if (response.Exception is not null || response.StatusCode is not (>= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices))
            {
                throw new StraumrException($"Source request '{request.Name}' failed while refreshing variable '{variable.Name}'" +
                    (response.StatusCode is { } status ? $" (HTTP {(int)status})" : string.Empty), StraumrError.InvalidEntry);
            }
            if (response.Warnings.Any(warning => warning.Contains("could not be resolved.", StringComparison.Ordinal)))
            {
                throw new StraumrException($"Source request '{request.Name}' contains unresolved references", StraumrError.MissingEntry);
            }
            string value = ResponseValueExtractor.Extract(response.Content ?? string.Empty, response.ResponseHeaders, source.ExtractionSource, source.Expression);
            StraumrVariable latest = await variableService.GetAsync(workspace, variable.Id, false, cancellationToken);
            if (await VariableFingerprintAsync(workspace, latest, cancellationToken, false) != configuration)
            {
                throw new StraumrException("The variable source or its inputs changed during refresh; refresh it again", StraumrError.EntryConflict);
            }
            latest.CachedValue = value;
            latest.CachedAt = DateTimeOffset.UtcNow;
            latest.CacheKey = await VariableFingerprintAsync(workspace, latest, cancellationToken);
            await variableService.SaveAsync(workspace, latest, cancellationToken);
            return value;
        }
        finally
        {
            if (ownsLock)
            {
                context.HasVariableLock = false;
                gate.Release();
            }
            context.Chain.RemoveAt(context.Chain.Count - 1);
        }
    }
    private static bool CacheExpired(StraumrVariable variable) =>
        variable.CachedAt is null || variable.Source?.TimeToLiveSeconds is { } seconds &&
        DateTimeOffset.UtcNow - variable.CachedAt.Value >= TimeSpan.FromSeconds(seconds);
    private async Task<string> VariableFingerprintAsync(StraumrWorkspaceEntry workspace, StraumrVariable variable, CancellationToken cancellationToken,
        bool includeDependencyCache = true)
    {
        JsonArray inputs = [Path.GetFullPath(workspace.Path), workspace.Id.ToString()];
        HashSet<string> visited = [];
        await VisitVariableAsync(variable, true);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inputs.ToJsonString())));
        async Task VisitVariableAsync(StraumrVariable current, bool root)
        {
            if (!visited.Add($"variable:{current.Id}"))
            {
                return;
            }
            JsonObject node = JsonSerializer.SerializeToNode(current, StraumrJsonContext.Default.StraumrVariable)!.AsObject();
            RemoveMetadata(node);
            node.Remove(nameof(StraumrVariable.CacheKey));
            if (root || !includeDependencyCache)
            {
                node.Remove(nameof(StraumrVariable.CachedValue));
                node.Remove(nameof(StraumrVariable.CachedAt));
            }
            else if (current.Source is not null)
            {
                node["Expired"] = CacheExpired(current);
            }
            inputs.Add(node);
            if (current.Source is { } source)
            {
                StraumrRequest request = await GetAsync(workspace, source.RequestId, false, cancellationToken);
                await VisitRequestAsync(request);
            }
        }
        async Task VisitRequestAsync(StraumrRequest request)
        {
            if (!visited.Add($"request:{request.Id}"))
            {
                return;
            }
            JsonObject node = JsonSerializer.SerializeToNode(request, StraumrJsonContext.Default.StraumrRequest)!.AsObject();
            RemoveMetadata(node);
            node.Remove(nameof(StraumrRequest.LastResponse));
            node.Remove(nameof(StraumrRequest.Captures));
            inputs.Add(node);
            await VisitReferencesAsync(node);
            if (request.AuthId is { } authId)
            {
                StraumrAuth auth = await authService.GetAsync(workspace, authId, false, cancellationToken);
                JsonObject authNode = JsonSerializer.SerializeToNode(auth, StraumrJsonContext.Default.StraumrAuth)!.AsObject();
                RemoveMetadata(authNode);
                JsonObject config = authNode[nameof(StraumrAuth.Config)]!.AsObject();
                if (auth.Config is OAuth2Config)
                {
                    config.Remove(nameof(OAuth2Config.Token));
                }
                if (auth.Config is CustomAuthConfig)
                {
                    config.Remove(nameof(CustomAuthConfig.CachedValue));
                }
                inputs.Add(authNode);
                JsonObject references = authNode.DeepClone().AsObject();
                if (auth.Config is CustomAuthConfig)
                {
                    JsonObject referenceConfig = references[nameof(StraumrAuth.Config)]!.AsObject();
                    string template = referenceConfig[nameof(CustomAuthConfig.ApplyHeaderTemplate)]!.GetValue<string>();
                    referenceConfig[nameof(CustomAuthConfig.ApplyHeaderTemplate)] = ReferencePattern.Replace(template, match =>
                        match.Groups["name"].Value.Trim().Equals(CustomAuthConfig.ValueName, StringComparison.OrdinalIgnoreCase) ? string.Empty : match.Value);
                }
                await VisitReferencesAsync(references);
            }
        }
        async Task VisitReferencesAsync(JsonNode node)
        {
            IEnumerable<string> texts = Strings(node);
            foreach (string name in texts.SelectMany(text => ReferencePattern.Matches(text).Select(match => match.Groups["name"].Value.Trim()))
                         .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
            {
                if (VariableHelpers.IsSecretName(name))
                {
                    StraumrSecret secret = await secretService.GetAsync(VariableHelpers.StripSecretPrefix(name), false, cancellationToken);
                    JsonObject secretNode = JsonSerializer.SerializeToNode(secret, StraumrJsonContext.Default.StraumrSecret)!.AsObject();
                    RemoveMetadata(secretNode);
                    inputs.Add(secretNode);
                }
                else
                {
                    await VisitVariableAsync(await variableService.GetAsync(workspace, name, false, cancellationToken), false);
                }
            }
        }
    }
    private static IEnumerable<string> Strings(JsonNode? node) => node switch
    {
        JsonObject document => document.SelectMany(field => Strings(field.Value)),
        JsonArray array => array.SelectMany(Strings),
        JsonValue value when value.TryGetValue(out string? text) => [text],
        _ => []
    };
    private static void RemoveMetadata(JsonObject node)
    {
        node.Remove(nameof(StraumrModelBase.Modified));
        node.Remove(nameof(StraumrModelBase.LastAccessed));
    }
    private async Task ValidateCapturesAsync(StraumrWorkspaceEntry workspace, StraumrRequest request, CancellationToken cancellationToken)
    {
        HashSet<Guid> targets = [];
        if (request.Captures is null || request.Captures.Any(capture => capture is null))
        {
            throw new StraumrException("Response captures must be an array of extraction rules", StraumrError.InvalidEntry);
        }
        foreach (ResponseCapture capture in request.Captures)
        {
            ResponseValueExtractor.Validate(capture.ExtractionSource, capture.Expression);
            if (!targets.Add(capture.VariableId))
            {
                throw new StraumrException("A response can capture each variable only once", StraumrError.InvalidEntry);
            }
            StraumrVariable variable = await variableService.GetAsync(workspace, capture.VariableId, false, cancellationToken);
            if (variable.Source is not null)
            {
                throw new StraumrException($"Capture target '{variable.Name}' must be a plain variable", StraumrError.InvalidEntry);
            }
        }
    }
    private async Task CaptureAsync(StraumrWorkspaceEntry workspace, StraumrRequest request, StraumrResponse response, CancellationToken cancellationToken)
    {
        if (request.Captures.Count == 0)
        {
            return;
        }
        SemaphoreSlim gate = _variableLocks.GetOrAdd(Path.GetFullPath(workspace.Path), _ => new SemaphoreSlim(1, 1));
        bool ownsLock = false;
        List<(StraumrVariable Original, StraumrVariable Updated)> changes = [];
        List<StraumrVariable> written = [];
        try
        {
            await gate.WaitAsync(cancellationToken);
            ownsLock = true;
            await ValidateCapturesAsync(workspace, request, cancellationToken);
            foreach (ResponseCapture capture in request.Captures)
            {
                StraumrVariable original = await variableService.GetAsync(workspace, capture.VariableId, false, cancellationToken);
                string value = ResponseValueExtractor.Extract(response.Content ?? string.Empty, response.ResponseHeaders,
                    capture.ExtractionSource, capture.Expression);
                StraumrVariable updated = original.CopyAs(original.Name);
                updated.Id = original.Id;
                updated.LastAccessed = original.LastAccessed;
                updated.Value = value;
                changes.Add((original, updated));
            }
            foreach ((StraumrVariable original, StraumrVariable updated) in changes)
            {
                await variableService.SaveAsync(workspace, updated, cancellationToken);
                written.Add(original);
            }
            _resolution.Value!.Notices.AddRange(changes.Select(change =>
                $"Captured response into variable '{change.Updated.Name}' ({change.Updated.Id})."));
        }
        catch (Exception exception)
        {
            List<Exception> failures = [];
            foreach (StraumrVariable original in written)
            {
                try
                {
                    await fileService.WriteStraumrModelAsync(variableService.PathFor(workspace, original.Id), original,
                        StraumrJsonContext.Default.StraumrVariable, false, CancellationToken.None);
                }
                catch (Exception failure)
                {
                    failures.Add(failure);
                }
            }
            throw new StraumrException("The HTTP request completed, but response capture failed: " + exception.Message +
                (failures.Count == 0 ? string.Empty : " Some variables could not be restored; check the capture targets before retrying."),
                StraumrError.InvalidEntry, failures.Count == 0 ? exception : new AggregateException([exception, .. failures]));
        }
        finally
        {
            if (ownsLock)
            {
                gate.Release();
            }
        }
    }
}
