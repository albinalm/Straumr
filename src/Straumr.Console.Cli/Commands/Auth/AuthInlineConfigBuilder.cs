using Straumr.Core.Enums;
using Straumr.Core.Models;

namespace Straumr.Console.Cli.Commands.Auth;

internal static class AuthInlineConfigBuilder
{
    internal static bool HasConfigMutationFlags(AuthInlineSettingsBase settings)
    {
        return settings.Type is not null ||
               settings.Secret is not null ||
               settings.Prefix is not null ||
               settings.Username is not null ||
               settings.Password is not null ||
               settings.GrantType is not null ||
               settings.TokenUrl is not null ||
               settings.ClientId is not null ||
               settings.ClientSecret is not null ||
               settings.Scope is not null ||
               settings.AuthorizationUrl is not null ||
               settings.RedirectUri is not null ||
               settings.Pkce is not null ||
               settings.CustomUrl is not null ||
               settings.CustomMethod is not null ||
               settings.CustomHeaders?.Length > 0 ||
               settings.CustomParams?.Length > 0 ||
               settings.CustomBody is not null ||
               settings.CustomBodyType is not null ||
               settings.ExtractionSource is not null ||
               settings.ExtractionExpression is not null ||
               settings.ApplyHeaderName is not null ||
               settings.ApplyHeaderTemplate is not null;
    }

    internal static StraumrAuthConfig? Build(
        AuthInlineSettingsBase settings,
        Action<string> writeError,
        StraumrAuthConfig? existingConfig = null)
    {
        string? effectiveType = settings.Type?.ToLowerInvariant() ?? TypeName(existingConfig);
        if (effectiveType is null)
        {
            writeError("A --type is required when editing auth config inline.");
            return null;
        }

        switch (effectiveType)
        {
            case "bearer":
            {
                BearerAuthConfig? existing = existingConfig as BearerAuthConfig;
                return new BearerAuthConfig
                {
                    Token = settings.Secret ?? existing?.Token ?? string.Empty,
                    Prefix = settings.Prefix ?? existing?.Prefix ?? "Bearer"
                };
            }
            case "basic":
            {
                BasicAuthConfig? existing = existingConfig as BasicAuthConfig;
                return new BasicAuthConfig
                {
                    Username = settings.Username ?? existing?.Username ?? string.Empty,
                    Password = settings.Password ?? existing?.Password ?? string.Empty
                };
            }
            case "oauth2":
            case "oauth2-client-credentials":
            case "oauth2-authorization-code":
            case "oauth2-password":
            {
                OAuth2Config? existing = existingConfig as OAuth2Config;
                OAuth2GrantType grantType = ResolveOAuth2GrantType(settings, existing);

                OAuth2Config oauth2 = new OAuth2Config
                {
                    GrantType = grantType,
                    TokenUrl = settings.TokenUrl ?? existing?.TokenUrl ?? string.Empty,
                    ClientId = settings.ClientId ?? existing?.ClientId ?? string.Empty,
                    ClientSecret = settings.ClientSecret ?? existing?.ClientSecret ?? string.Empty,
                    Scope = settings.Scope ?? existing?.Scope ?? string.Empty,
                    AuthorizationUrl = settings.AuthorizationUrl ?? existing?.AuthorizationUrl ?? string.Empty,
                    RedirectUri = settings.RedirectUri ?? existing?.RedirectUri ?? "http://localhost:8765/callback",
                    UsePkce = existing?.UsePkce ?? false,
                    CodeChallengeMethod = existing?.CodeChallengeMethod ?? "S256",
                    Username = settings.Username ?? existing?.Username ?? string.Empty,
                    Password = settings.Password ?? existing?.Password ?? string.Empty,
                    Token = existing?.Token
                };

                if (settings.Pkce is not null)
                {
                    oauth2.UsePkce = !settings.Pkce.Equals("disabled", StringComparison.OrdinalIgnoreCase);
                    oauth2.CodeChallengeMethod = settings.Pkce.Equals("plain", StringComparison.OrdinalIgnoreCase)
                        ? "plain"
                        : "S256";
                }

                return oauth2;
            }
            case "custom":
            {
                CustomAuthConfig? existing = existingConfig as CustomAuthConfig;
                CustomAuthConfig custom = new CustomAuthConfig
                {
                    Url = settings.CustomUrl ?? existing?.Url ?? string.Empty,
                    Method = settings.CustomMethod ?? existing?.Method ?? "POST",
                    BodyType = existing?.BodyType ?? BodyType.None,
                    Bodies = existing is null
                        ? new Dictionary<BodyType, string>()
                        : new Dictionary<BodyType, string>(existing.Bodies),
                    Headers = existing is null
                        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, string>(existing.Headers, StringComparer.OrdinalIgnoreCase),
                    Params = existing is null
                        ? new Dictionary<string, string>(StringComparer.Ordinal)
                        : new Dictionary<string, string>(existing.Params, StringComparer.Ordinal),
                    Source = existing?.Source ?? ExtractionSource.JsonPath,
                    ExtractionExpression = settings.ExtractionExpression ?? existing?.ExtractionExpression ?? string.Empty,
                    ApplyHeaderName = settings.ApplyHeaderName ?? existing?.ApplyHeaderName ?? "Authorization",
                    ApplyHeaderTemplate = settings.ApplyHeaderTemplate ?? existing?.ApplyHeaderTemplate ?? "Bearer {{value}}",
                    CachedValue = existing?.CachedValue
                };

                foreach (string header in settings.CustomHeaders ?? [])
                {
                    int colon = header.IndexOf(':');
                    if (colon < 0)
                    {
                        writeError($"Invalid header (expected \"Name: Value\"): {header}");
                        return null;
                    }

                    custom.Headers[header[..colon].Trim()] = header[(colon + 1)..].Trim();
                }

                foreach (string param in settings.CustomParams ?? [])
                {
                    int eq = param.IndexOf('=');
                    if (eq < 0)
                    {
                        writeError($"Invalid param (expected \"key=value\"): {param}");
                        return null;
                    }

                    custom.Params[param[..eq]] = param[(eq + 1)..];
                }

                if (settings.CustomBodyType is not null)
                {
                    BodyType bodyType = ParseBodyType(settings.CustomBodyType, writeError);
                    if (bodyType == BodyType.None && !settings.CustomBodyType.Equals("none", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    custom.BodyType = bodyType;
                }

                if (settings.CustomBody is not null)
                {
                    BodyType bodyType = settings.CustomBodyType is not null
                        ? ParseBodyType(settings.CustomBodyType, writeError)
                        : custom.BodyType == BodyType.None ? BodyType.Json : custom.BodyType;

                    if (bodyType == BodyType.None)
                    {
                        writeError("A custom auth body cannot use the none body type.");
                        return null;
                    }

                    custom.BodyType = bodyType;
                    custom.Bodies[bodyType] = settings.CustomBody;
                }

                if (settings.ExtractionSource is not null)
                {
                    ExtractionSource? source = settings.ExtractionSource.ToLowerInvariant() switch
                    {
                        "jsonpath" or "json" => ExtractionSource.JsonPath,
                        "header" => ExtractionSource.ResponseHeader,
                        "regex" => ExtractionSource.Regex,
                        _ => null
                    };

                    if (source is null)
                    {
                        writeError($"Unknown extraction source: {settings.ExtractionSource}. Use jsonpath, header, or regex.");
                        return null;
                    }

                    custom.Source = source.Value;
                }

                return custom;
            }
            default:
                writeError(
                    $"Unknown auth type: {settings.Type ?? effectiveType}. Use bearer, basic, oauth2, oauth2-client-credentials, oauth2-authorization-code, oauth2-password, or custom.");
                return null;
        }
    }

    private static string? TypeName(StraumrAuthConfig? config)
    {
        return config switch
        {
            BearerAuthConfig => "bearer",
            BasicAuthConfig => "basic",
            OAuth2Config => "oauth2",
            CustomAuthConfig => "custom",
            _ => null
        };
    }

    private static OAuth2GrantType ResolveOAuth2GrantType(AuthInlineSettingsBase settings, OAuth2Config? existing)
    {
        return settings.Type?.ToLowerInvariant() switch
        {
            "oauth2-client-credentials" => OAuth2GrantType.ClientCredentials,
            "oauth2-authorization-code" => OAuth2GrantType.AuthorizationCode,
            "oauth2-password" => OAuth2GrantType.ResourceOwnerPassword,
            _ => settings.GrantType?.ToLowerInvariant() switch
            {
                "client-credentials" or "client_credentials" => OAuth2GrantType.ClientCredentials,
                "authorization-code" or "authorization_code" => OAuth2GrantType.AuthorizationCode,
                "password" or "resource-owner-password" => OAuth2GrantType.ResourceOwnerPassword,
                _ => existing?.GrantType ?? OAuth2GrantType.ClientCredentials
            }
        };
    }

    private static BodyType ParseBodyType(string bodyTypeText, Action<string> writeError)
    {
        BodyType bodyType = bodyTypeText.ToLowerInvariant() switch
        {
            "json" => BodyType.Json,
            "xml" => BodyType.Xml,
            "text" => BodyType.Text,
            "form" => BodyType.FormUrlEncoded,
            "multipart" => BodyType.MultipartForm,
            "raw" => BodyType.Raw,
            "none" => BodyType.None,
            _ => (BodyType)(-1)
        };

        if ((int)bodyType == -1)
        {
            writeError($"Unknown body type: {bodyTypeText}. Use json, xml, text, form, multipart, raw, or none.");
            return BodyType.None;
        }

        return bodyType;
    }
}
