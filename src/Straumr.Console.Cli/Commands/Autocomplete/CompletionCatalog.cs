namespace Straumr.Console.Cli.Commands.Autocomplete;

internal static class CompletionCatalog
{
    public const string Cli = "cli";
    public const string Console = "console";

    public const string List = "list";
    public const string Create = "create";
    public const string Delete = "delete";
    public const string Edit = "edit";
    public const string Get = "get";
    public const string Use = "use";
    public const string Copy = "copy";
    public const string Import = "import";
    public const string Export = "export";
    public const string Config = "config";
    public const string Autocomplete = "autocomplete";
    public const string Send = "send";
    public const string About = "about";

    public const string Workspace = "workspace";
    public const string WorkspaceAlias = "ws";
    public const string Request = "request";
    public const string RequestAlias = "rq";
    public const string Auth = "auth";
    public const string AuthAlias = "au";
    public const string Secret = "secret";
    public const string SecretAlias = "sc";
    public const string WorkspacePath = "workspace-path";
    public const string Install = "install";
    public const string Query = "query";

    public static readonly string[] TopLevelVerbs =
    [
        List, Create, Delete, Edit, Get, Use, Copy, Import, Export,
        Config, Autocomplete, Send, About
    ];

    public static readonly IReadOnlyDictionary<string, string[]> VerbNouns =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [List] = [Workspace, Request, Auth, Secret],
            [Create] = [Workspace, Request, Auth, Secret],
            [Delete] = [Workspace, Request, Auth, Secret],
            [Edit] = [Workspace, Request, Auth, Secret],
            [Get] = [Workspace, Request, Auth, Secret],
            [Use] = [Workspace],
            [Copy] = [Workspace, Request, Auth, Secret],
            [Import] = [Workspace],
            [Export] = [Workspace],
            [Config] = [WorkspacePath],
            [Autocomplete] = [Install],
            [Send] = [],
            [About] = []
        };

    public static bool IsWorkspaceNoun(string value) =>
        value.Equals(Workspace, StringComparison.OrdinalIgnoreCase) ||
        value.Equals(WorkspaceAlias, StringComparison.OrdinalIgnoreCase);

    public static bool IsRequestNoun(string value) =>
        value.Equals(Request, StringComparison.OrdinalIgnoreCase) ||
        value.Equals(RequestAlias, StringComparison.OrdinalIgnoreCase);

    public static bool IsAuthNoun(string value) =>
        value.Equals(Auth, StringComparison.OrdinalIgnoreCase) ||
        value.Equals(AuthAlias, StringComparison.OrdinalIgnoreCase);

    public static bool IsSecretNoun(string value) =>
        value.Equals(Secret, StringComparison.OrdinalIgnoreCase) ||
        value.Equals(SecretAlias, StringComparison.OrdinalIgnoreCase);

    public static bool IsCliPrefix(string value) =>
        value.Equals(Cli, StringComparison.OrdinalIgnoreCase) ||
        value.Equals(Console, StringComparison.OrdinalIgnoreCase);
}
