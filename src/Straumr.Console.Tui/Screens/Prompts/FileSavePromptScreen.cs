using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Prompts.FileSave;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class FileSavePromptScreen : PromptScreen<string?>
{
    private readonly FileSavePrompt _prompt;

    public FileSavePromptScreen(
        string title,
        string? initialPath,
        IReadOnlyList<IAllowedType>? allowedTypes,
        StraumrTheme? theme = null,
        bool mustExist = false)
    {
        Add(new Banner
        {
            Theme = theme,
        });

        Add(new HintsBar
        {
            Text = "j/k Move  h Up  l/o Open  Enter Open/Select  / Filter  c Clear filter  t Type  p Go to  n New dir  D Delete  s Save  Tab Name  Esc Cancel",
        });

        _prompt = Add(new FileSavePrompt
        {
            Title = title,
            InitialPath = initialPath,
            AllowedTypes = allowedTypes,
            MustExist = mustExist,
            Theme = theme,
        });

        _prompt.SaveRequested += Complete;
        _prompt.CancelRequested += Cancel;
    }

    public override bool OnKeyDown(Key key)
    {
        if (_prompt.HandleGoToKeyDown(key))
        {
            return true;
        }

        if (_prompt.HandleFilterKeyDown(key))
        {
            return true;
        }

        return base.OnKeyDown(key);
    }
}
