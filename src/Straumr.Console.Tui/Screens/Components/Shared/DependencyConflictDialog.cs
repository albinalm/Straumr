using Straumr.Core.Enums;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class DependencyConflictDialog
{
    private readonly Dialog _dialog;
    public DependencyConflictDialog(string kind, string name, int remaining, Action<DependencyCopyAction, bool> select)
    {
        var apply = new CheckBox("Apply this choice to remaining variable conflicts", false);
        apply.SetStyle(StraumrStyleService.CheckBox);
        apply.IsVisible = kind == "variable" && remaining > 1;
        var cancel = new Button("Cancel") { AutoFocus = true };
        cancel.SetStyle(StraumrStyleService.Button);
        var reuse = new Button("Use existing");
        reuse.SetStyle(StraumrStyleService.PrimaryButton);
        var replace = new Button("Replace existing");
        replace.SetStyle(StraumrStyleService.DangerButton);
        var rename = new Button("Copy with new name");
        rename.SetStyle(StraumrStyleService.PrimaryButton);
        Button[] answers = [cancel, reuse, replace, rename];
        string detail = "Use its current configuration, replace it with the source configuration, or create a separately named copy. " +
                        "Replacing also updates it for other requests using it.";
        if (kind == "variable" && remaining > 1)
        {
            detail += $" {remaining} variable conflicts remain.";
        }
        foreach (Button answer in answers)
        {
            answer.KeyDown((_, e) => ConfirmDialog.MoveToAnotherAnswer(e, answers, answer));
        }
        VStack content = new VStack(
                new TextBlock($"The destination already has a {kind} named {name}.").Style(StraumrStyleService.BrightText).Wrap(true),
                new TextBlock(detail)
                    .Style(StraumrStyleService.MutedText).Wrap(true),
                apply,
                new HStack(answers).Spacing(1).HorizontalAlignment(Align.End))
            .Spacing(1).HorizontalAlignment(Align.Stretch);
        _dialog = StraumrDialogHelpers.Create(new TextBlock($"{kind} name conflict").Style(StraumrStyleService.AccentText), content, 78);
        cancel.Click(() => _dialog.Close());
        reuse.Click(() => Answer(DependencyCopyAction.UseExisting));
        replace.Click(() => Answer(DependencyCopyAction.Replace));
        rename.Click(() => Answer(DependencyCopyAction.Create));
        void Answer(DependencyCopyAction action)
        {
            _dialog.Close();
            select(action, apply.IsVisible && apply.IsChecked);
        }
    }
    public void Show() => TuiWindowHelpers.Show(_dialog);
}
