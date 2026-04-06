using System.Text.Json;

namespace Straumr.Cli.Models;

public record AuthGetResult(
    string Id,
    string Name,
    string Type,
    bool AutoRenewAuth,
    string LastAccessed,
    string Modified,
    JsonElement Config);
