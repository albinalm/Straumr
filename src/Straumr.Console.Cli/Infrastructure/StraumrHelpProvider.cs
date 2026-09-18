using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace Straumr.Console.Cli.Infrastructure;

internal sealed class StraumrHelpProvider : HelpProvider
{
    private static readonly (string Switches, string Description)[] GlobalOptions =
    [
        ("-h, --help", "Prints help information"),
        ("--version", "Prints the Straumr version"),
        ("--no-color", "Disables colored output"),
        (AgentDocs.Flag, AgentDocs.Description)
    ];

    private readonly ICommandAppSettings _settings;

    public StraumrHelpProvider(ICommandAppSettings settings)
        : base(settings)
    {
        _settings = settings;
    }

    public override IEnumerable<IRenderable> GetOptions(ICommandModel model, ICommandInfo? command)
    {
        if (command is not null)
        {
            return base.GetOptions(model, command);
        }

        OptionStyle? style = _settings.HelpProviderStyles?.Options;

        Grid grid = new();
        grid.AddColumn(new GridColumn { Padding = new Padding(4, 0, 0, 0), NoWrap = true });
        grid.AddColumn(new GridColumn { Padding = new Padding(4, 0, 0, 0) });

        foreach ((string switches, string description) in GlobalOptions)
        {
            grid.AddRow(
                Stylize(switches, style?.RequiredOption),
                Markup.Escape(description));
        }

        string heading = Stylize("OPTIONS:", style?.Header);

        return
        [
            new Markup(Environment.NewLine + heading + Environment.NewLine),
            grid
        ];
    }

    private static string Stylize(string text, Style? style)
    {
        string escaped = Markup.Escape(text);
        return style is null ? escaped : $"[{style.ToMarkup()}]{escaped}[/]";
    }
}
