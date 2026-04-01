using System.Text.Json.Serialization;
using Straumr.Core.Models;

namespace Straumr.Core.Configuration;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(StraumrWorkspace))]
[JsonSerializable(typeof(StraumrRequest))]
[JsonSerializable(typeof(StraumrSecret))]
[JsonSerializable(typeof(StraumrAuthTemplate))]
[JsonSerializable(typeof(StraumrOptions))]
[JsonSerializable(typeof(StraumrAuthConfig))]
[JsonSerializable(typeof(BearerAuthConfig))]
[JsonSerializable(typeof(BasicAuthConfig))]
[JsonSerializable(typeof(OAuth2Config))]
[JsonSerializable(typeof(OAuth2Token))]
[JsonSerializable(typeof(CustomAuthConfig))]
public partial class StraumrJsonContext : JsonSerializerContext;
