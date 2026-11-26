using Straumr.Console.Tui.Screens.Components.Shared;
using Straumr.Core.Configuration;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Helpers;

internal static class TabNavigationHelpers
{
    public static void AddTabNavigation(this Visual root, Action<int> change, Func<bool> available)
    {
        Add(root, "Straumr.NextTab", "Next tab", 1, CommandPresentation.CommandBar, change, available);
        Add(root, "Straumr.NextTabAlternate", "Next tab", 1, TuiKeybindHelpers.SecondaryPresentation("Straumr.NextTab"), change, available);
        Add(root, "Straumr.PreviousTab", "Previous tab", -1, CommandPresentation.CommandBar, change, available);
        Add(root, "Straumr.PreviousTabAlternate", "Previous tab", -1, TuiKeybindHelpers.SecondaryPresentation("Straumr.PreviousTab"), change, available);
    }

    public static void AddFieldTraversal(this Dialog dialog)
    {
        foreach ((string id, int step) in ((string, int)[])[("Straumr.NextField", 1), ("Straumr.PreviousField", -1)])
        {
            dialog.AddCommand(new Command
            {
                Id = id,
                LabelMarkup = step > 0 ? "Next field" : "Previous field",
                Gesture = TuiKeybindHelpers.Get(id),
                Importance = CommandImportance.Secondary,
                Presentation = CommandPresentation.CommandBar,
                Execute = _ => dialog.FocusStep(step)
            });
        }
        dialog.BlockFieldTraversal();
    }

    public static void BlockFieldTraversal(this Visual root)
    {
        // XenoAtom traverses focus on unhandled Tab even when the configured binding is disabled.
        foreach (TerminalModifiers modifiers in (TerminalModifiers[])[TerminalModifiers.None, TerminalModifiers.Shift])
        {
            var tab = new KeyGesture(TerminalKey.Tab, modifiers);
            root.AddCommand(new Command
            {
                Id = $"Straumr.BlockTab.{modifiers}",
                LabelMarkup = string.Empty,
                Gesture = tab,
                Presentation = CommandPresentation.None,
                CanExecute = _ => root.App?.FocusedElement is not CommandPromptInput || tab != TuiKeybindHelpers.Get("CommandPrompt.Complete"),
                ConsumesGestureWhenUnavailable = false,
                Execute = _ => { }
            });
        }
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
