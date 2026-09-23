using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class PagedPane
{
    private readonly State<int> _page;
    private readonly PagedPanePageModel[] _pages;

    private readonly Visual[] _tabs;

    public PagedPane(bool tabCyclesPages, params PagedPanePageModel[] pages)
    {
        _pages = pages;
        _page = new State<int>(FirstApplicable(0));
        for (int index = 0; index < _pages.Length; index++)
        {
            _pages[index].Content.IsVisible = index == SelectedPage;
        }

        Root = new ZStack(_pages.Select(page => page.Content).ToArray())
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        _tabs = _pages.Select(BuildTab).ToArray();
        SyncTabs();

        TabRule = StraumrSurfaceHelpers.HorizontalDivider();
        TabRule.StartLabel(new HStack(_tabs).Spacing(0));

        if (tabCyclesPages)
        {
            Root.AddCommand(CycleCommand("NextTab", TuiKeybindHelpers.CombinedLabel("Next tab", "PagedPane.NextTabKey"),
                CommandPresentation.CommandBar, 1));
            Root.AddCommand(CycleCommand("PreviousTab", "Previous tab",
                CommandPresentation.None, -1));
            Root.AddCommand(CycleCommand("NextTabKey", "Next tab", TuiKeybindHelpers.SecondaryPresentation("PagedPane.NextTab"), 1));
        }
        else
        {
            Root.AddCommand(CycleCommand("NextPage", "Next page",
                CommandPresentation.CommandBar, 1));
            Root.AddCommand(CycleCommand("NextPageLetter", "Next page",
                CommandPresentation.None, 1));
        }
    }

    public Visual Root { get; }

    public Rule TabRule { get; }

    public int SelectedPage => _page.Value;

    public Visual FocusTarget => _pages[Math.Clamp(SelectedPage, 0, _pages.Length - 1)].FocusTarget();

    public Visual Page(int index) => _pages[index].Content;

    public void Select(int index)
    {
        if (index == _page.Value || !_pages[index].Applies)
        {
            return;
        }

        bool owned = Root.Owns();
        for (int page = 0; page < _pages.Length; page++)
        {
            _pages[page].Content.IsVisible = page == index;
        }

        _page.Value = index;
        if (owned)
        {
            Focus();
        }
    }

    public void Sync()
    {
        Select(FirstApplicable(_page.Value));
        SyncTabs();
    }

    private void SyncTabs()
    {
        for (int index = 0; index < _tabs.Length; index++)
        {
            _tabs[index].IsVisible = _pages[index].Applies;
        }
    }

    private int FirstApplicable(int preferred)
    {
        if ((uint)preferred < (uint)_pages.Length && _pages[preferred].Applies)
        {
            return preferred;
        }

        int index = Array.FindIndex(_pages, page => page.Applies);
        return index < 0 ? 0 : index;
    }

    public void Focus()
    {
        Visual target = FocusTarget;
        if (target.App is { } attached)
        {
            attached.Focus(target);
            return;
        }

        if (Root.App is not { } app)
        {
            return;
        }

        app.Focus(Root);
        app.Post(() =>
        {
            Visual prepared = FocusTarget;
            prepared.App?.Focus(prepared);
        });
    }

    private Command CycleCommand(string id, string label,
        CommandPresentation presentation, int step) =>
        new()
        {
            Id = $"PagedPane.{id}",
            LabelMarkup = label,
            Gesture = TuiKeybindHelpers.Get($"PagedPane.{id}"),
            Importance = CommandImportance.Secondary,
            Presentation = presentation,
            CanExecute = _ => !Root.IsTyping(),
            IsVisible = _ => !Root.IsTyping(),
            ConsumesGestureWhenUnavailable = false,
            Execute = _ => Change(Step(step))
        };

    private void Change(int index)
    {
        Select(index);
        Focus();
    }

    private int Step(int step)
    {
        int index = SelectedPage;
        for (int moved = 0; moved < _pages.Length; moved++)
        {
            index = (index + step + _pages.Length) % _pages.Length;
            if (_pages[index].Applies)
            {
                return index;
            }
        }

        return SelectedPage;
    }

    private Visual BuildTab(PagedPanePageModel page, int index)
    {
        var tab = new ChromeButton(page.Title);
        tab.SetStyle(() => index == SelectedPage
            ? Root.Owns() ? StraumrStyleService.RuleTabFocused : StraumrStyleService.RuleTabSelected
            : StraumrStyleService.RuleTab);
        tab.Click(() => Change(index));
        return tab;
    }
}
