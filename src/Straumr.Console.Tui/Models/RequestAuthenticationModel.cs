using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Models;

internal sealed record RequestAuthenticationModel(
    string Source,
    string Type,
    string Injects,
    AuthStatusModel Status,
    IReadOnlyList<ReferenceModel> References,
    string? Problem)
{
    public static readonly RequestAuthenticationModel Loading =
        new("Loading…", "", "", new AuthStatusModel("", StraumrStyleService.MutedText), [], null);

    public static async Task<RequestAuthenticationModel> LoadAsync(
        StraumrRequest request, StraumrWorkspaceEntry workspace,
        IStraumrAuthService authService, IStraumrSecretService secretService,
        IStraumrVariableService variableService, CancellationToken cancellationToken)
    {
        StraumrAuth? auth = null;
        string? problem = null;
        if (request.AuthId is { } id)
        {
            try
            {
                auth = await authService.GetAsync(workspace, id, false, cancellationToken);
            }
            catch (Exception exception) when (RequestScreen.IsRecoverable(exception))
            {
                problem = exception.Message;
            }
        }

        IReadOnlyList<ReferenceModel> references = await ReferenceHelpers.ResolveAsync(
            secretService, variableService, workspace, request, auth, RequestScreen.IsRecoverable, cancellationToken);

        bool direct = request.Headers.Keys.Any(name => name.Equals("Authorization", StringComparison.OrdinalIgnoreCase));
        return new RequestAuthenticationModel(
            auth?.Name ?? (request.AuthId.HasValue ? $"Missing auth {request.AuthId.ToString()![..8]}" : direct ? "Request header" : "None"),
            auth is not null ? AuthFormatting.TypeName(auth.Config) : direct ? "Direct" : "None",
            auth is not null ? AuthFormatting.Injects(auth.Config) : direct ? "Authorization" : "None",
            auth is not null
                ? AuthFormatting.Status(auth)
                : new AuthStatusModel(direct ? "Configured" : "No authentication", StraumrStyleService.MutedText),
            references,
            problem);
    }
}
