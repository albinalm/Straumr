using System.Diagnostics;
using Straumr.Console.Tui.Formatting;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Animation;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Layout;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Visuals.Shared.Editor;

/// <summary>
/// The screen a resource is created or changed on. It knows nothing about what it is editing: a
/// caller hands it the pages, what the bar should say about the resource, and a way to save.
/// Requests use it now; an auth's four shapes and a secret's two fields are the same view with a
/// different set of pages.
/// </summary>
/// <remarks>
/// It is built from the shell's own pieces, as the full-screen response is — the identity header
/// naming the resource, a three-row bar, the page titles notched into the rule that closes it, a
/// pane, and the one-row footer — so the room it takes goes to the fields rather than to chrome of
/// its own.
/// </remarks>
internal sealed class ResourceEditorView
{
    private static readonly TimeSpan NoticeLifetime = TimeSpan.FromSeconds(5);

    /// <summary>How long the marker stays green after a save.</summary>
    private static readonly TimeSpan SavedLifetime = TimeSpan.FromSeconds(1);

    /// <summary>The C0 control character a terminal sends for <c>Ctrl</c> plus a letter.</summary>
    private const char SaveControlChar = (char)('S' & 0x1F);

    private readonly Dialog _dialog;
    private readonly PagedPane _pages;
    private readonly EditorForm[] _forms;
    private readonly Action _save;
    private readonly Action _closed;
    private readonly Func<string> _name;

    /// <summary>
    /// What the header calls the resource, mirrored out of the state once per update pass. The name
    /// is read from the state being edited, which is a plain object the binding graph knows nothing
    /// about, so a header bound straight to it never changed as the name was typed.
    /// </summary>
    private readonly State<string> _headerName;
    private readonly State<bool> _dirty;

    /// <summary>
    /// Whether saving still creates the resource. It stops being true the moment one does, because
    /// the view stays open afterwards and the next save has to change what the first one wrote.
    /// </summary>
    private readonly State<bool> _isNew;

    /// <summary>Whether a save has just landed, which the marker shows green for a moment.</summary>
    private readonly State<bool> _justSaved = new(false);

    private SavedFlash? _flash;
    private readonly State<bool> _saving = new(false);
    private readonly State<string> _notice = new(string.Empty);
    private readonly State<bool> _noticeError = new(false);
    private DateTimeOffset _noticeUntil;
    private bool _focused;
    private bool _suspended;

    /// <summary>Where focus goes when the view comes back, when that is not the page's entry point.</summary>
    private Visual? _resumeFocus;

    /// <param name="summary">
    /// The bar's left half: what the resource is, live, as the fields change it. A request shows its
    /// method and URL there, which is the pair a reader checks before saving.
    /// </param>
    /// <param name="isNew">
    /// Whether saving creates the resource rather than changing one. It decides what the bar says
    /// before anything has been typed, and whether closing with edits is worth asking about.
    /// </param>
    /// <param name="hasChanges">
    /// Whether what the fields hold still differs from what was opened. Asked after every edit
    /// rather than latched on the first one, so a value typed and then typed back reads as saved
    /// again and closing stops asking about work that no longer exists. Only the caller can answer
    /// it: this view does not know what a resource is, let alone when two of them are the same.
    /// </param>
    /// <param name="save">
    /// Asked for, not performed. Core calls belong on the screen's update loop, where every other one
    /// in this app runs; the screen answers through <see cref="Saved"/> or <see cref="Failed"/>.
    /// </param>
    /// <param name="sourceName">
    /// What a copy was copied from, or <see langword="null"/> for anything that is not a copy. A copy
    /// opens with its name cleared, because the one thing it must be given is a name of its own — and
    /// the name a reader wants to base that on is the one they just left. It goes on the bar rather
    /// than into a field: it is not editable, and it has to survive moving to another page.
    /// </param>
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
                // Everything a field keeps in a shape of its own goes into the state first, or the
                // comparison would be made against a value the reader has already changed.
                foreach (EditorForm page in _forms)
                    page.Commit();
                _dirty.Value = _isNew.Value || hasChanges();
            };
        }

        _pages = new PagedPane(tabCyclesPages: false,
            _forms.Select(form => new PagedPanePage(form.Title, form.Root, () => form.FocusTarget)
            {
                Visible = () => form.Applies
            }).ToArray());

        var content = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() }, new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto })
            .Cell(StraumrHeader.Create(() => _headerName.Value, workspaceName), 0, 0)
            .Cell(StraumrSurfaces.HorizontalDivider(), 1, 0)
            .Cell(BuildBar(summary, sourceName), 2, 0)
            .Cell(_pages.TabRule, 3, 0)
            .Cell(ResourceScreenLayout.Pane(_pages.Root), 4, 0)
            .Cell(StraumrSurfaces.HorizontalDivider(), 5, 0)
            .Cell(BuildFooter(), 6, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        _dialog = StraumrDialog.CreateScreen(content);
        AddSaveCommands();
        _dialog.AddCommand(new Command
        {
            Id = "Editor.Close",
            LabelMarkup = "Back",
            Gesture = new KeyGesture(TerminalKey.Escape),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => !_saving.Value,
            IsVisible = _ => !_saving.Value,
            ConsumesGestureWhenUnavailable = false,
            Execute = _ => Close()
        });
    }

    public void Show()
    {
        _dialog.Show();
        FocusPage();
    }

    public void Update()
    {
        Resume();
        _headerName.Value = _name();
        // Before the focus pass, and on every pass rather than only on a change: a discriminator is
        // read from the state being edited, which anything holding that state may have written to.
        // The pages go with the fields, because the same discriminator can decide a whole page: an
        // auth's type leaves a bearer token with no grant flow and no request of its own.
        foreach (EditorForm form in _forms)
            form.Sync();
        _pages.Sync();
        FocusPage();
        if (_notice.Value.Length > 0 && DateTimeOffset.UtcNow >= _noticeUntil)
            _notice.Value = string.Empty;
    }

    /// <summary>
    /// Gives the terminal up to another program — the reader's editor — without losing the work on
    /// this screen. The state being edited belongs to the caller and outlives the app; only the view
    /// has to be put down and picked up again.
    /// </summary>
    /// <remarks>
    /// Taking the screen down is not optional. A shown window stays parented to the app that showed
    /// it, that app ends when the loop stops for the external program, and a window still parented to
    /// it is refused by the next one as already being part of a UI tree. Closing here is what leaves
    /// it free to be shown again.
    /// </remarks>
    public void Suspend()
    {
        if (_suspended)
            return;

        _suspended = true;
        // The field the reader left is the field they come back to. Holding the visual across the
        // two apps is safe; asking it for focus between them is not, because it has no app then.
        _resumeFocus = _dialog.App?.FocusedElement;
        _dialog.Close();
    }

    /// <summary>
    /// Says something on the view's own footer, for an outcome the caller produced on this screen's
    /// behalf. The shell's message line is behind this view, where nobody would read it.
    /// </summary>
    public void Report(string message, bool error) => Notify(message, error);

    /// <summary>
    /// Validates every page and, if they all pass, asks the screen to save. The first page holding a
    /// problem is brought forward with that field focused, because a message about a field nobody can
    /// see names a place rather than showing one.
    /// </summary>
    public void TrySave()
    {
        if (_saving.Value)
            return;

        foreach (EditorForm form in _forms)
            form.Commit();

        for (int index = 0; index < _forms.Length; index++)
        {
            if (_forms[index].Validate() is not { } problem)
                continue;

            _pages.Select(index);
            _forms[index].Validate();
            Notify(problem, error: true);
            return;
        }

        _saving.Value = true;
        _save();
    }

    /// <remarks>
    /// The view closes and says nothing. What was saved is reported on the screen behind it, which is
    /// where the reader is looking a moment later and where the saved resource is now selected; a
    /// message on a surface that is going away is one nobody reads.
    /// </remarks>
    public void Saved()
    {
        _saving.Value = false;
        _dirty.Value = false;
        // What was created exists now, so the next save changes it rather than making a second one.
        _isNew.Value = false;
        _justSaved.Value = true;
        _flash?.Arm(SavedLifetime);
    }

    /// <summary>
    /// Core refused the save. The view stays open holding the work, and says why on its own footer:
    /// a refusal is about a field on this screen, and closing would discard what has to be corrected.
    /// </summary>
    public void Failed(string message)
    {
        _saving.Value = false;
        Notify(message, error: true);
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
            destructive: true,
            () =>
            {
                _dialog.Close();
                _closed();
            }).Show();
    }

    /// <remarks>
    /// The app running now is not the one this view was shown on, so the view is shown again rather
    /// than revealed. It happens on the update pass because that is the first moment there is an app
    /// to be shown on, and the page and the fields are the retained ones, so the reader comes back to
    /// the page they left with everything they had typed still on it.
    /// </remarks>
    private void Resume()
    {
        if (!_suspended)
            return;

        _suspended = false;
        _focused = false;
        _dialog.Show();
    }

    /// <remarks>
    /// A screen opens with a region focused, and the fields are the region this one is about. Focus
    /// is asked for once the dialog has an app to ask — <c>AutoFocus</c> is not enough, because the
    /// dialog takes the focus pass that follows its own <c>Show</c>.
    /// A view coming back from an external program goes to the field it was on rather than to the
    /// page's entry point: the reader left from the body and was handed back the type above it.
    /// </remarks>
    private void FocusPage()
    {
        if (_focused || _pages.FocusTarget.App is not { } app)
            return;

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

    /// <remarks>
    /// The bar keeps its three rows whether the resource is being edited or written, so saving
    /// changes what the right half says rather than shifting the screen under the reader.
    /// </remarks>
    /// <remarks>
    /// The marker goes green the moment a save lands and grey a second later. Saving no longer
    /// closes the view, so something has to say that it happened; a colour that fades says it
    /// without taking a row or needing to be dismissed, and what it fades to is the same "saved"
    /// the marker would have read anyway.
    /// </remarks>
    private Visual BuildBar(Visual summary, string? sourceName)
    {
        Visual status = new HStack(
                new TextBlock(() => _saving.Value ? "●" : _dirty.Value ? "●" : "○")
                    .Style(() => _saving.Value
                        ? StraumrStyles.AccentText
                        : _dirty.Value
                            ? StraumrStyles.AmberText
                            : _justSaved.Value
                                ? StraumrStyles.GreenText
                                : StraumrStyles.MutedText),
                new TextBlock(() => _saving.Value
                        ? "saving"
                        : _dirty.Value
                            ? _isNew.Value ? "not created yet" : "unsaved changes"
                            : "saved")
                    .Style(() => _saving.Value
                        ? StraumrStyles.AccentText
                        : _justSaved.Value
                            ? StraumrStyles.GreenText
                            : StraumrStyles.MutedBrightText)
                    .Trimming(TextTrimming.EndEllipsis))
            .Spacing(1);

        _flash = new SavedFlash(status, _justSaved);

        // Beside the marker rather than left of the summary: both are facts about this editing
        // session rather than about the resource, and the summary's half is the one that has to give
        // way on a narrow terminal. It is labelled the way the workspace copy dialog labels the same
        // thing, so a copy says `Source` wherever it is made.
        Visual right = sourceName is null
            ? _flash
            : new HStack(
                    new TextBlock("Source").Style(StraumrStyles.MutedText),
                    new TextBlock(SecretFormatting.Display(sourceName)).Style(StraumrStyles.PrimaryText),
                    _flash)
                .Spacing(2);

        return StraumrSurfaces.Inset(
            StraumrSurfaces.Bar(summary, right),
            ResourceScreenLayout.PaneInset);
    }

    /// <remarks>
    /// One row holding one thing at a time, as the shell's footer does: the shortcut hints, or a
    /// result until it expires.
    /// </remarks>
    private Visual BuildFooter()
    {
        // Wrapped rather than clipped; see StraumrTuiApp's footer.
        var hints = new CommandBar { MultiLine = true }.Style(StraumrStyles.CommandBar);
        return new ZStack(
                StraumrSurfaces.Inset(hints, StraumrSurfaces.RowInset)
                    .IsVisible(() => _notice.Value.Length == 0),
                StraumrSurfaces.Inset(
                        new TextBlock(() => _notice.Value)
                            .Style(() => _noticeError.Value ? StraumrStyles.RedText : StraumrStyles.GreenText)
                            .Trimming(TextTrimming.EndEllipsis)
                            .HorizontalAlignment(Align.Stretch),
                        StraumrSurfaces.RowInset)
                    .IsVisible(() => _notice.Value.Length > 0))
            .HorizontalAlignment(Align.Stretch);
    }

    /// <remarks>
    /// Every letter is a character a focused field would swallow, so the one action that cannot be a
    /// letter is the one that leaves the form. A terminal sends <c>Ctrl</c> and a letter as the
    /// single C0 byte the letter maps to, so the gesture has to carry that control character; the
    /// letter-and-modifier form is registered beside it, unpresented, for a host that reports the two
    /// separately.
    /// </remarks>
    private void AddSaveCommands()
    {
        _dialog.AddCommand(SaveCommand("Editor.Save", SaveControlChar, CommandPresentation.CommandBar));
        _dialog.AddCommand(SaveCommand("Editor.Save.Letter", 's', CommandPresentation.None));
    }

    private Command SaveCommand(string id, char gestureChar, CommandPresentation presentation) => new()
    {
        Id = id,
        LabelMarkup = "Save",
        Gesture = new KeyGesture(gestureChar, TerminalModifiers.Ctrl),
        Importance = CommandImportance.Primary,
        Presentation = presentation,
        CanExecute = _ => !_saving.Value,
        IsVisible = _ => !_saving.Value,
        ConsumesGestureWhenUnavailable = false,
        Execute = _ => TrySave()
    };

    /// <summary>
    /// Holds the status marker and turns its green back to grey once the moment has passed.
    /// </summary>
    /// <remarks>
    /// A timer is needed because the update pass runs on input and on animation, and a save is
    /// followed by neither: the marker would have stayed green until the next keystroke. The
    /// framework's animation scheduler is the clock every other timed thing in this app uses, and
    /// asking for no tick at all while nothing is pending costs nothing when nothing is.
    /// </remarks>
    private sealed class SavedFlash : Visual, IAnimatedVisual
    {
        private readonly Visual _content;
        private readonly State<bool> _flag;
        private long _until;

        public SavedFlash(Visual content, State<bool> flag)
        {
            _content = content;
            _flag = flag;
            AttachChild(content);
        }

        public void Arm(TimeSpan lifetime) =>
            _until = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * lifetime.TotalSeconds);

        public long NextAnimationTick => _flag.Value ? _until : long.MaxValue;

        public bool AdvanceAnimation(long timestamp)
        {
            if (!_flag.Value || timestamp < _until)
                return false;

            _flag.Value = false;
            return true;
        }

        protected override int ChildrenCount => 1;

        protected override Visual GetChild(int index) =>
            index == 0 ? _content : throw new ArgumentOutOfRangeException(nameof(index));

        protected override SizeHints MeasureCore(in LayoutConstraints constraints) =>
            _content.Measure(constraints);

        protected override void ArrangeCore(in Rectangle finalRect) => _content.Arrange(finalRect);
    }
}
