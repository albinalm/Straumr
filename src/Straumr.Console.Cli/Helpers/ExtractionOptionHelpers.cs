using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Helpers;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Console.Cli.Helpers;

internal static class ExtractionOptionHelpers
{
    public static async Task<List<ResponseCapture>> ParseCapturesAsync(IEnumerable<string> rules, StraumrWorkspaceEntry workspace,
        IStraumrVariableService variables, CancellationToken cancellationToken = default)
    {
        List<ResponseCapture> captures = [];
        foreach (string rule in rules)
        {
            int separator = rule.IndexOf('=');
            if (separator <= 0)
            {
                throw new StraumrException("Use --capture <variable-name-or-id>=<extraction-rule>", StraumrError.InvalidEntry);
            }
            StraumrVariable variable = await GetVariableAsync(variables, workspace, rule[..separator], cancellationToken: cancellationToken);
            (ExtractionSource source, string expression) = Parse(rule[(separator + 1)..]);
            captures.Add(new ResponseCapture { VariableId = variable.Id, ExtractionSource = source, Expression = expression });
        }
        return captures;
    }
    public static (ExtractionSource Source, string Expression) Parse(string option)
    {
        int separator = option.IndexOf(':');
        if (separator <= 0)
        {
            throw new StraumrException("Use jsonpath:<path>, header:<name>, or regex:<pattern> for extraction", StraumrError.InvalidEntry);
        }
        ExtractionSource source = option[..separator].ToLowerInvariant() switch
        {
            "jsonpath" => ExtractionSource.JsonPath,
            "header" => ExtractionSource.ResponseHeader,
            "regex" => ExtractionSource.Regex,
            _ => throw new StraumrException("Unknown extraction source; use jsonpath, header, or regex", StraumrError.InvalidEntry)
        };
        string expression = option[(separator + 1)..];
        ResponseValueExtractor.Validate(source, expression);
        return (source, expression);
    }
}
