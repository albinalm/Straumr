using System.Text.Json.Serialization;
using Straumr.Core.Enums;

namespace Straumr.Core.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "authType")]
[JsonDerivedType(typeof(BearerAuthConfig), nameof(AuthType.Bearer))]
[JsonDerivedType(typeof(BasicAuthConfig), nameof(AuthType.Basic))]
[JsonDerivedType(typeof(OAuth2Config), nameof(AuthType.OAuth2))]
[JsonDerivedType(typeof(CustomAuthConfig), nameof(AuthType.Custom))]
public abstract class StraumrAuthConfig
{
    public abstract AuthType Type { get; }
}