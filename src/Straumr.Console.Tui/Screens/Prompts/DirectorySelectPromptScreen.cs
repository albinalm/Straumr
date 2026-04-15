using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Prompts.FileSave;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class DirectorySelectPromptScreen : PromptScreen<string?>
{
    private readonly DirectorySelectPrompt _prompt;

    public DirectorySelectPromptScreen(string title, string? initialPath, StraumrTheme? theme = null)
    {
        Add(new Banner
        {
            Theme = theme,
        });

        Add(new HintsBar
        {
            Text = "j/k Navigate  h Up  l/o Open  / Filter  c Clear filter  p Go to  n New dir  D Delete  s Select  Esc Cancel",
        });

        _prompt = Add(new DirectorySelectPrompt
        {
            Title = title,
            InitialPath = initialPath,
            Theme = theme,
        });

        _prompt.DirectorySelected += Complete;
        _prompt.CancelRequested += Cancel;
    }

    public override bool OnKeyDown(Key key)
    {
        if (_prompt.HandleFilterKeyDown(key))
        {
            return true;
        }

        return base.OnKeyDown(key);
    }
}
