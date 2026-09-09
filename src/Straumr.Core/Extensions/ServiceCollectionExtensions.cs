using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Straumr.Core.Models;
using Straumr.Core.Services;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStraumrCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IStraumrFileService, StraumrFileService>();
        services.TryAddSingleton<IStraumrOptionsService, StraumrOptionsService>();
        services.TryAddSingleton<IStraumrWorkspaceService, StraumrWorkspaceService>();
        services.TryAddSingleton<IStraumrAuthService, StraumrAuthService>();
        services.TryAddSingleton<IStraumrRequestService, StraumrRequestService>();
        services.TryAddSingleton<IStraumrSecretService, StraumrSecretService>();

        services.AddHttpClient();
        AddRequestClient(services, StraumrHttpClientNames.Default, followRedirects: false, insecure: false);
        AddRequestClient(services, StraumrHttpClientNames.Redirects, followRedirects: true, insecure: false);
        AddRequestClient(services, StraumrHttpClientNames.Insecure, followRedirects: false, insecure: true);
        AddRequestClient(services, StraumrHttpClientNames.InsecureRedirects, followRedirects: true, insecure: true);

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

internal static class StraumrHttpClientNames
{
    public const string Default = "straumr-request";
    public const string Redirects = "straumr-request-redirects";
    public const string Insecure = "straumr-request-insecure";
    public const string InsecureRedirects = "straumr-request-insecure-redirects";

    public static string For(SendOptions? options)
    {
        return (options?.Insecure == true, options?.FollowRedirects == true) switch
        {
            (false, false) => Default,
            (false, true) => Redirects,
            (true, false) => Insecure,
            (true, true) => InsecureRedirects
        };
    }
}
