using System.Text.Json;
using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Core.Configuration;
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
    AuthStatus Status,
    IReadOnlyList<SecretReference> References,
    string? Problem)
{
    public static readonly RequestAuthentication Loading =
        new("Loading…", "", "", new AuthStatus("", StraumrStyles.MutedText), [], null);

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

        // A request's references are its own and its auth's: both are substituted on the same send,
        // so both have to resolve for it to reach the wire.
        var documents = new List<string>
        {
            JsonSerializer.Serialize(request, StraumrJsonContext.Default.StraumrRequest)
        };
        if (auth is not null)
            documents.Add(JsonSerializer.Serialize(auth, StraumrJsonContext.Default.StraumrAuth));
        IReadOnlyList<SecretReference> references = await SecretReferences.ResolveAsync(
            secretService, documents, RequestScreen.IsRecoverable, cancellationToken);

        bool direct = request.Headers.Keys.Any(name => name.Equals("Authorization", StringComparison.OrdinalIgnoreCase));
        return new RequestAuthentication(
            auth?.Name ?? (request.AuthId.HasValue ? $"Missing auth {request.AuthId.ToString()![..8]}" : direct ? "Request header" : "None"),
            auth is not null ? AuthFormatting.TypeName(auth.Config) : direct ? "Direct" : "None",
            auth is not null ? AuthFormatting.Injects(auth.Config) : direct ? "Authorization" : "None",
            // Said in the Auths screen's words, because it is the same auth and the same question.
            auth is not null
                ? AuthFormatting.Status(auth)
                : new AuthStatus(direct ? "Configured" : "No authentication", StraumrStyles.MutedText),
            references,
            problem);
    }
}
