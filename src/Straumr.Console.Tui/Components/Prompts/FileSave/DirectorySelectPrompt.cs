using Straumr.Console.Tui.Enums;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts.FileSave;

internal sealed class DirectorySelectPrompt : FileSystemPromptBase
{
    public event Action<string>? DirectorySelected;

    private Label? _selectionLabel;

    protected override int FooterReservedRows => 4;

    protected override void BuildContent(FrameView frame)
    {
        _selectionLabel = new Label
        {
            X = 1,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(2),
            Text = "Select a directory and press s or Enter to confirm.",
        };

        frame.Add(_selectionLabel);
        UpdateSelectionLabel();
    }

    protected override bool ShouldIncludeEntry(FileBrowserEntry entry)
        => entry.Kind != FileEntryKind.File;

    protected override bool HandleCustomKey(Key key)
    {
        int ch = KeyHelpers.GetCharValue(key);
        if (ch is 's' or 'S')
        {
            ConfirmSelection();
            return true;
        }

        if (key == Key.Enter)
        {
            ConfirmSelection();
            return true;
        }

        return false;
    }

    protected override void OnFileEntryActivated(FileBrowserEntry entry)
    {
        // Ignore file activation; only directories are selectable.
    }

    protected override void OnAfterDirectoryChanged()
    {
        UpdateSelectionLabel();
    }

    private void ConfirmSelection()
    {
        string path = CurrentDirectory;
        FileBrowserEntry? entry = GetSelectedEntry();
        if (entry is not null && entry.Kind == FileEntryKind.Directory)
        {
            path = entry.FullPath;
        }

        DirectorySelected?.Invoke(path);
    }

    private void UpdateSelectionLabel()
    {
        _selectionLabel?.Text = $"Current directory: {CurrentDirectory}";
    }
}
