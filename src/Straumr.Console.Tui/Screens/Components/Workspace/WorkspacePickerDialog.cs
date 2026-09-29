using Straumr.Console.Tui.Screens.Components.Shared;
using Straumr.Core.Models;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using ResourceList = Straumr.Console.Tui.Screens.Components.Shared.ResourceList;

namespace Straumr.Console.Tui.Screens.Components.Workspace;

internal sealed class WorkspacePickerDialog
{
    private readonly Dialog _dialog;
    private readonly ResourceFilter _filter;
    private readonly ResourceList _list;
    private readonly Action<StraumrWorkspace> _select;
    private readonly IReadOnlyList<StraumrWorkspace> _workspaces;
    private List<StraumrWorkspace> _visible = [];
    public WorkspacePickerDialog(string entityName, IReadOnlyList<StraumrWorkspace> workspaces, Action<StraumrWorkspace> select)
    {
        _workspaces = workspaces;
        _select = select;
        _list = new ResourceList([], new TextBlock("No workspaces match this filter.").Style(StraumrStyleService.MutedText), "Copy here")
        {
            AutoFocus = true
        };
        _list.ItemActivated += Choose;
        _filter = new ResourceFilter("filter workspaces", ApplyFilter, () => _list);
        _filter.AttachCommands(_list);
        var cancel = new Button("Cancel");
        cancel.SetStyle(StraumrStyleService.Button);
        var confirm = new Button("Copy here");
        confirm.SetStyle(StraumrStyleService.PrimaryButton);
        confirm.IsEnabled(() => (uint)_list.SelectedIndex < (uint)_visible.Count);
        confirm.Click(() => Choose(_list.SelectedIndex));
        var hints = new HintBar();
        hints.SetStyle(StraumrStyleService.CommandBar);
        VStack content = new VStack(
                new TextBlock($"Choose a destination for {entityName}").Style(StraumrStyleService.MutedText).Wrap(true),
                _filter.Root,
                ResourceScreenLayoutHelpers.Scrollable(_list).MinHeight(3).MaxHeight(12),
                hints,
                new HStack(cancel, confirm).Spacing(1).HorizontalAlignment(Align.End))
            .Spacing(1).HorizontalAlignment(Align.Stretch);
        _dialog = StraumrDialogHelpers.Create(new TextBlock("Copy to workspace").Style(StraumrStyleService.AccentText), content, 72);
        cancel.Click(() => _dialog.Close());
        ApplyFilter(string.Empty);
    }
    public void Show() => TuiWindowHelpers.Show(_dialog);
    private void ApplyFilter(string query)
    {
        _visible = _workspaces.Where(workspace => workspace.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        _list.SetRows(_visible.Select(workspace => new ResourceRowModel(workspace.Name)));
        _list.SelectedIndex = _visible.Count > 0 ? 0 : -1;
    }
    private void Choose(int index)
    {
        if ((uint)index >= (uint)_visible.Count)
        {
            return;
        }
        _dialog.Close();
        _select(_visible[index]);
    }
}
