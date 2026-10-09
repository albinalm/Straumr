using Straumr.Core.Configuration;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;

namespace Straumr.Console.Tui.Helpers;

internal static class TabNavigationHelpers
{
    public static void AddTabNavigation(this Visual root, Action<int> change, Func<bool> available)
    {
        Add(root, "Straumr.NextTab", "Next tab", 1, CommandPresentation.CommandBar, change, available);
        Add(root, "Straumr.NextTabAlternate", "Next tab", 1, TuiKeybindHelpers.SecondaryPresentation("Straumr.NextTab"), change, available);
        Add(root, "Straumr.PreviousTab", "Previous tab", -1, CommandPresentation.CommandBar, change, available);
    }
    private static void Add(Visual root, string id, string label, int step, CommandPresentation presentation, Action<int> change, Func<bool> available)
    {
        bool CanNavigate() => available() && (!root.IsTyping() || StraumrKeybinds.Get(id) is { Modifiers: ConsoleModifiers modifiers } &&
            (modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) != 0);
        foreach (string commandId in (string[])[id, id + ".Letter"])
        {
            root.AddCommand(new Command
            {
                Id = commandId,
                LabelMarkup = label,
                Gesture = TuiKeybindHelpers.Get(commandId),
                Importance = CommandImportance.Secondary,
                Presentation = commandId == id ? presentation : CommandPresentation.None,
                CanExecute = _ => CanNavigate(),
                IsVisible = _ => CanNavigate(),
                ConsumesGestureWhenUnavailable = false,
                Execute = _ => change(step)
            });
        }
    }
}
