using System.Text.Json;
using Straumr.Core.Configuration;
using Straumr.Core.Helpers;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Screens.Request;

/// <param name="References">
/// Every secret the request or its auth refers to, and whether each one resolves. Held as the
/// references they are rather than as one joined string, because the region showing them puts each
/// on its own row and colours the ones that would fail a send.
/// </param>
internal sealed record RequestAuthentication(
    string Source,
    string Type,
    string Injects,
    string Status,
    IReadOnlyList<SecretReference> References,
    string? Problem)
{
    public static readonly RequestAuthentication Loading = new("Loading…", "", "", "", [], null);

    public static async Task<RequestAuthentication> LoadAsync(
        StraumrRequest request, StraumrWorkspaceEntry workspace,
        IStraumrAuthService authService, IStraumrSecretService secretService,
        CancellationToken cancellationToken)
    {
        StraumrAuth? auth = null;
        string? problem = null;
        if (request.AuthId is { } id)
        {
            try
            {
                auth = await authService.GetAsync(workspace, id, updateLastAccessed: false, cancellationToken);
            }
            catch (Exception exception) when (RequestScreen.IsRecoverable(exception))
            {
                problem = exception.Message;
            }
        }

        string referenceText = JsonSerializer.Serialize(request, StraumrJsonContext.Default.StraumrRequest);
        if (auth is not null)
            referenceText += JsonSerializer.Serialize(auth, StraumrJsonContext.Default.StraumrAuth);
        var references = new List<SecretReference>();
        foreach (string name in SecretHelpers.SecretPattern.Matches(referenceText)
                     .Select(match => match.Groups["name"].Value).Distinct(StringComparer.Ordinal))
        {
            try
            {
                await secretService.GetAsync(name, updateLastAccessed: false, cancellationToken);
                references.Add(new SecretReference(name, Available: true));
            }
            catch (Exception exception) when (RequestScreen.IsRecoverable(exception))
            {
                references.Add(new SecretReference(name, Available: false));
            }
        }

        bool direct = request.Headers.Keys.Any(name => name.Equals("Authorization", StringComparison.OrdinalIgnoreCase));
        return new RequestAuthentication(
            auth?.Name ?? (request.AuthId.HasValue ? $"Missing auth {request.AuthId.ToString()![..8]}" : direct ? "Request header" : "None"),
            auth?.Config.Type.ToString() ?? (direct ? "Direct" : "None"),
            auth?.Config is CustomAuthConfig custom ? custom.ApplyHeaderName : auth is not null || direct ? "Authorization" : "None",
            auth?.Config switch
            {
                OAuth2Config { Token: null } => auth.AutoRenewAuth ? "Token fetched on send" : "No token; auto-renew off",
                OAuth2Config { Token.IsExpired: true } => auth.AutoRenewAuth ? "Expired; renews on send" : "Expired; auto-renew off",
                OAuth2Config { Token.ExpiresAt: { } expiry } => $"Expires {expiry.ToLocalTime():g}",
                OAuth2Config => "Token available; no expiry",
                CustomAuthConfig { CachedValue: null } => "Fetched on send",
                CustomAuthConfig => "Cached value available",
                _ => auth is not null || direct ? "Configured" : "No authentication"
            },
            references,
            problem);
    }
}

/// <summary>One <c>{{secret:name}}</c> a request depends on, and whether the store can supply it.</summary>
internal sealed record SecretReference(string Name, bool Available);
