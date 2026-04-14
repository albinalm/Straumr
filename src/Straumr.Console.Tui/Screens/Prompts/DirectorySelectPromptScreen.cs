using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Prompts.FileSave;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class DirectorySelectPromptScreen : PromptScreen<string?>
{
    public DirectorySelectPromptScreen(string title, string? initialPath, StraumrTheme? theme = null)
    {
        Add(new Banner
        {
            Theme = theme,
        });

        Add(new HintsBar
        {
            Text = "j/k Navigate  g/G Bounds  h Up dir  l/o Open  / Filter  p Go to dir  s Select  Esc Cancel",
        });

        DirectorySelectPrompt prompt = Add(new DirectorySelectPrompt
        {
            Title = title,
            InitialPath = initialPath,
            Theme = theme,
        });

        prompt.DirectorySelected += Complete;
        prompt.CancelRequested += Cancel;
    }
}
