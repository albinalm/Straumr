using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Prompts.FileSave;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class FileSavePromptScreen : PromptScreen<string?>
{
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
            Text = "j/k Move  h Up  l/o Open  / Filter  t Type  p Go to  n New dir  D Delete  s Save  Tab Name  Esc Cancel",
        });

        FileSavePrompt prompt = Add(new FileSavePrompt
        {
            Title = title,
            InitialPath = initialPath,
            AllowedTypes = allowedTypes,
            MustExist = mustExist,
            Theme = theme,
        });

        prompt.SaveRequested += Complete;
        prompt.CancelRequested += Cancel;
    }
}
