namespace Straumr.Console.Cli.Commands.Autocomplete;

internal readonly record struct AutocompleteParsedQueryModel(IReadOnlyList<string> Tokens, bool AtNewToken);
