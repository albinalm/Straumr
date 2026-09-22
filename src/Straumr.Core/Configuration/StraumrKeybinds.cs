using System.Collections.ObjectModel;

namespace Straumr.Core.Configuration;

public static class StraumrKeybinds
{

    private static Dictionary<string, StraumrKeyGesture?> _bindings;
    public static IReadOnlyDictionary<string, string> Defaults { get; } = new ReadOnlyDictionary<string, string>(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Straumr.OpenCommandPrompt"] = ":",
            ["Straumr.Interrupt"] = "Ctrl+C",
            ["Straumr.FocusPrevious"] = "Ctrl+Tab",
            ["Straumr.FocusNext"] = "Tab",
            ["Straumr.FocusPreviousTab"] = "Shift+Tab",
            ["CommandPrompt.Accept"] = "Enter",
            ["CommandPrompt.Cancel"] = "Escape",
            ["CommandPrompt.Complete"] = "Tab",
            ["CommandPrompt.HistoryPrevious"] = "Up",
            ["CommandPrompt.HistoryNext"] = "Down",
            ["ResourceList.Next"] = "j",
            ["ResourceList.Previous"] = "k",
            ["ResourceList.First"] = "g",
            ["ResourceList.Last"] = "G",
            ["ResourceList.Activate"] = "Enter",
            ["ResourceList.Up"] = "Up",
            ["ResourceList.Down"] = "Down",
            ["ResourceList.Home"] = "Home",
            ["ResourceList.End"] = "End",
            ["ResourceList.PageUp"] = "PageUp",
            ["ResourceList.PageDown"] = "PageDown",
            ["ScrollableContent.Next"] = "j",
            ["ScrollableContent.Previous"] = "k",
            ["ScrollableContent.Top"] = "g",
            ["ScrollableContent.Bottom"] = "G",
            ["ScrollableContent.Up"] = "Up",
            ["ScrollableContent.Down"] = "Down",
            ["ScrollableContent.Home"] = "Home",
            ["ScrollableContent.End"] = "End",
            ["ScrollableContent.PageUp"] = "PageUp",
            ["ScrollableContent.PageDown"] = "PageDown",
            ["Select.Next"] = "j",
            ["Select.Previous"] = "k",
            ["Select.First"] = "g",
            ["Select.Last"] = "G",
            ["ResourceFilter.Open"] = "/",
            ["ResourceFilter.Accept"] = "Enter",
            ["ResourceFilter.Cancel"] = "Escape",
            ["ResourceScreen.ResizeLeft"] = "Ctrl+H",
            ["ResourceScreen.ResizeRight"] = "Ctrl+L",
            ["ResourceScreen.ResizeUp"] = "Ctrl+K",
            ["ResourceScreen.ResizeDown"] = "Ctrl+J",
            ["PreviewPane.NextTab"] = "t",
            ["PagedPane.NextTab"] = "Tab",
            ["PagedPane.PreviousTab"] = "Shift+Tab",
            ["PagedPane.NextTabKey"] = "t",
            ["PagedPane.NextPage"] = "Ctrl+T",
            ["Workspace.Create"] = "c",
            ["Workspace.Edit"] = "e",
            ["Workspace.Copy"] = "y",
            ["Workspace.Delete"] = "d",
            ["Workspace.Import"] = "i",
            ["Workspace.Export"] = "x",
            ["Request.New"] = "c",
            ["Request.Edit"] = "e",
            ["Request.Copy"] = "y",
            ["Request.Delete"] = "d",
            ["Request.EditJson"] = "Ctrl+E",
            ["Request.Send"] = "s",
            ["Request.Fullscreen"] = "v",
            ["Auth.New"] = "c",
            ["Auth.Edit"] = "e",
            ["Auth.Copy"] = "y",
            ["Auth.Delete"] = "d",
            ["Auth.EditJson"] = "Ctrl+E",
            ["Auth.Fetch"] = "f",
            ["Auth.Cancel"] = "Escape",
            ["Auth.ExtractHelp"] = "h",
            ["Auth.ExtractHelp.Function"] = "F1",
            ["Secret.New"] = "c",
            ["Secret.Edit"] = "e",
            ["Secret.Copy"] = "y",
            ["Secret.Delete"] = "d",
            ["Secret.EditJson"] = "Ctrl+E",
            ["Response.Send"] = "s",
            ["Response.Cancel"] = "Escape",
            ["Response.Back"] = "Escape",
            ["ResponseBody.Format"] = "b",
            ["ResponseBody.Highlight"] = "h",
            ["ResponseBody.Copy"] = "y",
            ["Editor.Save"] = "Ctrl+S",
            ["Editor.Close"] = "Escape",
            ["ContentField.Edit"] = "Enter",
            ["ContentField.Edit.Control"] = "Ctrl+E",
            ["KeyValueField.Add"] = "a",
            ["KeyValueField.Edit"] = "e",
            ["KeyValueField.Remove"] = "d",
            ["KeyValuePairDialog.Submit"] = "Enter",
            ["SecretSuggestions.Next"] = "Down",
            ["SecretSuggestions.Previous"] = "Up",
            ["SecretSuggestions.Insert"] = "Enter",
            ["SecretSuggestions.Dismiss"] = "Escape",
            ["StraumrDialog.Cancel"] = "Escape",
            ["ConfirmDialog.Cancel"] = "Escape",
            ["ConfirmDialog.Left"] = "Left",
            ["ConfirmDialog.Right"] = "Right",
            ["ConfirmDialog.Up"] = "Up",
            ["ConfirmDialog.Down"] = "Down",
            ["TextPromptDialog.Submit"] = "Enter",
            ["WorkspaceFormDialog.Submit"] = "Enter",
            ["BrowserDialog.Up"] = "Backspace",
            ["BrowserDialog.Select"] = "s",
            ["BrowserDialog.Select.Current"] = "Ctrl+Enter",
            ["BrowserDialog.New"] = "n",
            ["BrowserDialog.Rename"] = "r",
            ["BrowserDialog.Delete"] = "d",
            ["BrowserDialog.Cancel"] = "Escape",
            ["BrowserDialog.Location"] = "Ctrl+L",
            ["BrowserDialog.Location.Go"] = "Enter",
            ["BrowserDialog.Location.Cancel"] = "Escape",
            ["BrowserDialog.Location.Complete"] = "Tab",
            ["Cli.Cancel"] = "Escape",
            ["Cli.Next"] = "j",
            ["Cli.NextUpper"] = "J",
            ["Cli.Previous"] = "k",
            ["Cli.PreviousUpper"] = "K",
            ["Cli.First"] = "g",
            ["Cli.Last"] = "G",
            ["Cli.Search"] = "/"
        });
    static StraumrKeybinds() => _bindings = ResolveDefaults();
    public static int Version { get; private set; }
    public static StraumrKeyGesture? Get(string id) => _bindings[id];
    public static string Hint(string id) => Get(id)?.ToString() ?? "unbound";
    public static char? Echo(string id) => Get(id)?.Echo;

    public static string? Apply(IReadOnlyDictionary<string, string> overrides, string? preset = null)
    {
        Dictionary<string, StraumrKeyGesture?> bindings = ResolveDefaults();
        string? problem = null;
        if (!StraumrKeybindPresets.TryGet(preset, out IReadOnlyDictionary<string, string> defaults))
        {
            problem = $"settings.toml: unknown keybind-preset \"{preset}\"; using vim";
        }

        foreach ((string id, string value) in defaults)
        {
            bindings[id] = value == "none" ? null : StraumrKeyGesture.TryParse(value, out StraumrKeyGesture key) ? key
                : throw new InvalidOperationException($"Invalid preset binding: {id}");
        }

        foreach ((string id, string value) in overrides)
        {
            if (!bindings.ContainsKey(id))
            {
                problem ??= $"settings.toml: unknown keybind \"{id}\" (see docs/keybinds.md)";
                continue;
            }
            if (string.Equals(value?.Trim(), "default", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(value?.Trim(), "none", StringComparison.OrdinalIgnoreCase))
            {
                bindings[id] = null;
            }
            else if (StraumrKeyGesture.TryParse(value, out StraumrKeyGesture gesture))
            {
                bindings[id] = gesture;
            }
            else
            {
                problem ??= $"settings.toml: invalid keybind \"{id}\" = \"{value}\"; using its default";
            }
        }
        if (bindings.Any(pair => _bindings[pair.Key] != pair.Value))
        {
            Version++;
        }

        _bindings = bindings;
        return problem;
    }

    private static Dictionary<string, StraumrKeyGesture?> ResolveDefaults() => Defaults.ToDictionary(
        pair => pair.Key,
        pair => StraumrKeyGesture.TryParse(pair.Value, out StraumrKeyGesture gesture)
            ? (StraumrKeyGesture?)gesture : throw new InvalidOperationException($"Invalid default: {pair.Key}"),
        StringComparer.Ordinal);
}
