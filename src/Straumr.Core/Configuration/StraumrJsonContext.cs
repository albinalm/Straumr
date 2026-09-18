using System.Text.Json;
using System.Text.Json.Serialization;
using Straumr.Core.Models;

namespace Straumr.Core.Configuration;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(StraumrWorkspace))]
[JsonSerializable(typeof(StraumrRequest))]
[JsonSerializable(typeof(StraumrStoredResponse))]
[JsonSerializable(typeof(StraumrSecret))]
[JsonSerializable(typeof(StraumrAuth))]
[JsonSerializable(typeof(StraumrState))]
[JsonSerializable(typeof(StraumrPaneLayout))]
[JsonSerializable(typeof(StraumrAuthConfig))]
[JsonSerializable(typeof(BearerAuthConfig))]
[JsonSerializable(typeof(BasicAuthConfig))]
[JsonSerializable(typeof(OAuth2Config))]
[JsonSerializable(typeof(OAuth2Token))]
[JsonSerializable(typeof(CustomAuthConfig))]
public partial class StraumrJsonContext : JsonSerializerContext;
