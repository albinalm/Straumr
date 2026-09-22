using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class ResourceEditorView
{
    private static readonly TimeSpan NoticeLifetime = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan SavedLifetime = TimeSpan.FromSeconds(1);
    private readonly Action _closed;

    private readonly Dialog _dialog;
    private readonly State<bool> _dirty;
    private readonly EditorForm[] _forms;

    private readonly State<string> _headerName;

    private readonly State<bool> _isNew;

    private readonly State<bool> _justSaved = new(false);
    private readonly Func<string> _name;
    private readonly State<string> _notice = new(string.Empty);
    private readonly State<bool> _noticeError = new(false);
    private readonly PagedPane _pages;
    private readonly Action _save;
    private readonly State<bool> _saving = new(false);

    private SavedFlashVisual? _flash;
    private bool _focused;
    private DateTimeOffset _noticeUntil;

    private Visual? _resumeFocus;
    private bool _suspended;

    public ResourceEditorView(
        Func<string> name,
        Func<string?> workspaceName,
        Visual summary,
        bool isNew,
        EditorForm[] pages,
        Action save,
        Action closed,
        Func<bool> hasChanges,
        string? sourceName = null)
    {
        _forms = pages;
        _save = save;
        _closed = closed;
        _name = name;
        _headerName = new State<string>(name());
        _isNew = new State<bool>(isNew);
        _dirty = new State<bool>(isNew);

        foreach (EditorForm form in _forms)
        {
            form.Changed += () =>
            {
                foreach (EditorForm page in _forms)
                {
                    page.Commit();
                }

                _dirty.Value = _isNew.Value || hasChanges();
            };
        }

        _pages = new PagedPane(false,
            _forms.Select(form => new PagedPanePageModel(form.Title, form.Root, () => form.FocusTarget)
            {
                Visible = () => form.Applies
            }).ToArray());

        Grid content = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() }, new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto })
            .Cell(StraumrHeaderHelpers.Create(() => _headerName.Value, workspaceName), 0, 0)
            .Cell(StraumrSurfaceHelpers.HorizontalDivider(), 1, 0)
            .Cell(BuildBar(summary, sourceName), 2, 0)
            .Cell(_pages.TabRule, 3, 0)
            .Cell(ResourceScreenLayoutHelpers.Pane(_pages.Root), 4, 0)
            .Cell(StraumrSurfaceHelpers.HorizontalDivider(), 5, 0)
            .Cell(BuildFooter(), 6, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        _dialog = StraumrDialogHelpers.CreateScreen(content);
        AddSaveCommands();
        _dialog.AddCommand(new Command
        {
            Id = "Editor.Close",
            LabelMarkup = "Back",
            Gesture = TuiKeybindHelpers.Get("Editor.Close"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => !_saving.Value,
            IsVisible = _ => !_saving.Value,
            ConsumesGestureWhenUnavailable = false,
            Execute = _ => Close()
        });
    }

    public void Show() => TuiWindowHelpers.Show(_dialog, FocusPage);

    public void Update()
    {
        Resume();
        _headerName.Value = _name();
        foreach (EditorForm form in _forms)
        {
            form.Sync();
        }

        _pages.Sync();
        FocusPage();
        if (_notice.Value.Length > 0 && DateTimeOffset.UtcNow >= _noticeUntil)
        {
            _notice.Value = string.Empty;
        }
    }

    public void Suspend()
    {
        if (_suspended)
        {
            return;
        }

        _suspended = true;
        _resumeFocus = _dialog.App?.FocusedElement;
        _dialog.Close();
    }

    public void Report(string message, bool error) => Notify(message, error);

    public void TrySave()
    {
        if (_saving.Value)
        {
            return;
        }

        foreach (EditorForm form in _forms)
        {
            form.Commit();
        }

        for (int index = 0; index < _forms.Length; index++)
        {
            if (_forms[index].Validate() is not { } problem)
            {
                continue;
            }

            _pages.Select(index);
            _forms[index].Validate();
            Notify(problem, true);
            return;
        }

        _saving.Value = true;
        _save();
    }

    public void Saved()
    {
        _saving.Value = false;
        _dirty.Value = false;
        _isNew.Value = false;
        _justSaved.Value = true;
        _flash?.Arm(SavedLifetime);
    }

    public void Failed(string message)
    {
        _saving.Value = false;
        Notify(message, true);
    }

    private void Close()
    {
        if (!_dirty.Value)
        {
            _dialog.Close();
            _closed();
            return;
        }

        new ConfirmDialog(
            "Discard changes",
            "Discard your changes?",
            "Nothing typed here has been written yet. Closing loses it.",
            "Discard",
            true,
            () =>
            {
                _dialog.Close();
                _closed();
            }).Show();
    }

    private void Resume()
    {
        if (!_suspended)
        {
            return;
        }

        _suspended = false;
        _focused = false;
        TuiWindowHelpers.Show(_dialog);
    }

    private void FocusPage()
    {
        if (_focused || _pages.FocusTarget.App is not { } app)
        {
            return;
        }

        _focused = true;
        Visual target = _resumeFocus ?? _pages.FocusTarget;
        _resumeFocus = null;
        app.Focus(target);
    }

    private void Notify(string message, bool error)
    {
        _notice.Value = message;
        _noticeError.Value = error;
        _noticeUntil = DateTimeOffset.UtcNow + NoticeLifetime;
    }

    private Visual BuildBar(Visual summary, string? sourceName)
    {
        Visual status = new HStack(
                new TextBlock(() => _saving.Value ? "●" : _dirty.Value ? "●" : "○")
                    .Style(() => _saving.Value
                        ? StraumrStyleService.AccentText
                        : _dirty.Value
                            ? StraumrStyleService.AmberText
                            : _justSaved.Value
                                ? StraumrStyleService.GreenText
                                : StraumrStyleService.MutedText),
                new TextBlock(() => _saving.Value
                        ? "saving"
                        : _dirty.Value
                            ? _isNew.Value ? "not created yet" : "unsaved changes"
                            : "saved")
                    .Style(() => _saving.Value
                        ? StraumrStyleService.AccentText
                        : _justSaved.Value
                            ? StraumrStyleService.GreenText
                            : StraumrStyleService.MutedBrightText)
                    .Trimming(TextTrimming.EndEllipsis))
            .Spacing(1);

        _flash = new SavedFlashVisual(status, _justSaved);

        Visual right = sourceName is null
            ? _flash
            : new HStack(
                    new TextBlock("Source").Style(StraumrStyleService.MutedText),
                    new TextBlock(SecretFormatting.Display(sourceName)).Style(StraumrStyleService.PrimaryText),
                    _flash)
                .Spacing(2);

        return StraumrSurfaceHelpers.Inset(
            StraumrSurfaceHelpers.Bar(summary, right),
            ResourceScreenLayoutHelpers.PaneInset);
    }

    private Visual BuildFooter()
    {
        HintBar hints = new HintBar().Style(StraumrStyleService.CommandBar);
        return new ZStack(
                StraumrSurfaceHelpers.Inset(hints, StraumrSurfaceHelpers.RowInset)
                    .IsVisible(() => _notice.Value.Length == 0),
                StraumrSurfaceHelpers.Inset(
                        new TextBlock(() => _notice.Value)
                            .Style(() => _noticeError.Value ? StraumrStyleService.RedText : StraumrStyleService.GreenText)
                            .Trimming(TextTrimming.EndEllipsis)
                            .HorizontalAlignment(Align.Stretch),
                        StraumrSurfaceHelpers.RowInset)
                    .IsVisible(() => _notice.Value.Length > 0))
            .HorizontalAlignment(Align.Stretch);
    }

    private void AddSaveCommands()
    {
        _dialog.AddCommand(SaveCommand("Editor.Save", CommandPresentation.CommandBar));
        _dialog.AddCommand(SaveCommand("Editor.Save.Letter", CommandPresentation.None));
    }

    private Command SaveCommand(string id, CommandPresentation presentation) => new()
    {
        Id = id,
        LabelMarkup = "Save",
        Gesture = TuiKeybindHelpers.Get(id),
        Importance = CommandImportance.Primary,
        Presentation = presentation,
        CanExecute = _ => !_saving.Value,
        IsVisible = _ => !_saving.Value,
        ConsumesGestureWhenUnavailable = false,
        Execute = _ => TrySave()
    };
}
