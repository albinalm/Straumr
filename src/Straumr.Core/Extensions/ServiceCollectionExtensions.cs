using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Straumr.Core.Services;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStraumrCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IStraumrFileService, StraumrFileService>();
        services.TryAddSingleton<IStraumrStateService, StraumrStateService>();
        services.TryAddSingleton<IStraumrSettingsService, StraumrSettingsService>();
        services.TryAddSingleton<IStraumrWorkspaceService, StraumrWorkspaceService>();
        services.TryAddSingleton<IStraumrAuthService, StraumrAuthService>();
        services.TryAddSingleton<IStraumrRequestService, StraumrRequestService>();
        services.TryAddSingleton<IStraumrSecretService, StraumrSecretService>();

        services.AddHttpClient();
        AddRequestClient(services, StraumrHttpClientConstants.Default, false, false);
        AddRequestClient(services, StraumrHttpClientConstants.Redirects, true, false);
        AddRequestClient(services, StraumrHttpClientConstants.Insecure, false, true);
        AddRequestClient(services, StraumrHttpClientConstants.InsecureRedirects, true, true);

        return services;
    }

    private static void AddRequestClient(
        IServiceCollection services,
        string name,
        bool followRedirects,
        bool insecure)
    {
        services.AddHttpClient(name)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = followRedirects,
                ServerCertificateCustomValidationCallback = insecure
                    ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    : null
            });
    }
}
