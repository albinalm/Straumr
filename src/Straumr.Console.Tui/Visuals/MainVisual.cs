using Microsoft.Extensions.DependencyInjection;
using Straumr.Core.Services.Interfaces;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Figlet;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Visuals;

public sealed class MainVisual : Visual
{
    private readonly IStraumrOptionsService _optionsService;
    private readonly IStraumrRequestService _requestService;

    private readonly PromptEditor _commandEditor;
    private readonly Border _commandBorder;
    private readonly Group _requestsGroup;
    private readonly Visual _content;

    private RequestList? _requestList;
    private bool _requestsLoaded;

    public bool ExitRequested { get; private set; }

    public MainVisual(IServiceProvider serviceProvider)
    {
        _optionsService =
            serviceProvider.GetRequiredService<IStraumrOptionsService>();

        _requestService =
            serviceProvider.GetRequiredService<IStraumrRequestService>();

        HorizontalAlignment = Align.Stretch;
        VerticalAlignment = Align.Stretch;
        Focusable = true;
        IsTabStop = false;

        //
        // Top left
        //

        var keybindsSection = new VStack(
                new TextBlock("KEYBINDS CHEAT SHEET")
            )
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        //
        // Top right
        //
        var straumrFiglet = new TextFiglet("{str}")
            .Font(FigletPredefinedFont.Ogre)
            .LetterSpacing(0)
            .HorizontalAlignment(Align.End)
            .VerticalAlignment(Align.Center);

        var topSection = new Grid()
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch)
            .Columns(
                new ColumnDefinition
                {
                    Width = GridLength.Star()
                },
                new ColumnDefinition
                {
                    Width = GridLength.Auto
                }
            )
            .Rows(
                new RowDefinition
                {
                    Height = GridLength.Star()
                }
            )
            .ColumnGap(2)
            .Cell(keybindsSection, 0, 0)
            .Cell(straumrFiglet, 0, 1);

        //
        // Command prompt
        //

        _commandEditor = new PromptEditor()
            .PromptMarkup(string.Empty)
            .LineMode(PromptEditorLineMode.SingleLine)
            .EnterMode(PromptEditorEnterMode.EnterAccepts)
            .EscapeBehavior(PromptEditorEscapeBehavior.CancelPromptOrCompletion)
            .Accepted((_, args) => SubmitCommand(args.Text))
            .Canceled(CloseCommandPrompt);

        _commandBorder = new Border(_commandEditor)
            .HorizontalAlignment(Align.Stretch)
            .IsVisible(false);

        //
        // Bottom requests group
        //

        _requestsGroup = new Group("Requests")
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch)
            .Content(
                new HStack(
                        new Spinner(),
                        new TextBlock("Loading requests...")
                    )
                    .Spacing(1)
            )
            .Padding(new Thickness(0));

        //
        // Main layout
        //
        // ┌────────────────┬───────────┐
        // │ Keybinds       │ Straumr   │ 30%
        // ├────────────────┴───────────┤
        // │ :command                   │
        // ├────────────────────────────┤
        // │ Requests                   │
        // │                            │ 70%
        // └────────────────────────────┘
        //

        _content = new Grid()
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch)
            .Columns(
                new ColumnDefinition
                {
                    Width = GridLength.Star()
                }
            )
            .Rows(
                new RowDefinition
                {
                    Height = GridLength.Star(3)
                },
                new RowDefinition
                {
                    Height = GridLength.Auto
                },
                new RowDefinition
                {
                    Height = GridLength.Star(7)
                }
            )
            .RowGap(1)
            .Cell(topSection, 0, 0)
            .Cell(_commandBorder, 1, 0)
            .Cell(_requestsGroup, 2, 0);

        AttachChild(_content);
    }

    public async Task LoadRequestsAsync(
        CancellationToken cancellationToken)
    {
        if (_requestsLoaded)
            return;

        _requestsLoaded = true;
        await _optionsService.LoadAsync(cancellationToken);
        var workspace = _optionsService.Options.CurrentWorkspace;

        if (workspace is null)
        {
            _requestsGroup.Content(
                new TextBlock("No workspace selected.")
            );

            return;
        }

        try
        {
            var requests = await _requestService.ListAsync(
                workspace,
                cancellationToken: cancellationToken);

            _requestList = new RequestList(requests)
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch);

            _requestsGroup.Content(_requestList);

            if (!_commandBorder.IsVisible)
                App?.Focus(_requestList);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _requestsGroup.Content(
                new TextBlock(
                    $"Failed to load requests: {ex.Message}")
            );
        }
    }

    public void DismissCommandPromptIfUnfocused()
    {
        if (_commandBorder.IsVisible &&
            !_commandEditor.HasFocus &&
            !_commandEditor.HasFocusWithin)
        {
            CloseCommandPrompt(restoreFocus: false);
        }
    }

    protected override int ChildrenCount => 1;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_commandBorder.IsVisible && e.Char == ':')
        {
            OpenCommandPrompt();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void OpenCommandPrompt()
    {
        _commandEditor.Text = string.Empty;
        _commandBorder.IsVisible = true;
        App?.Focus(_commandEditor);
    }

    private void CloseCommandPrompt() =>
        CloseCommandPrompt(restoreFocus: true);

    private void CloseCommandPrompt(bool restoreFocus)
    {
        _commandEditor.Text = string.Empty;
        _commandBorder.IsVisible = false;

        if (restoreFocus)
            App?.Focus(_requestList is null ? this : _requestList);
    }

    private void SubmitCommand(string command)
    {
        if (string.Equals(
                command.Trim().TrimStart(':'),
                "q",
                StringComparison.OrdinalIgnoreCase))
        {
            ExitRequested = true;
        }

        CloseCommandPrompt();
    }

    protected override Visual GetChild(int index) =>
        index == 0
            ? _content
            : throw new ArgumentOutOfRangeException(nameof(index));
}
