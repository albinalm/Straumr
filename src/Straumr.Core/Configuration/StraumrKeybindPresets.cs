using System.Collections.ObjectModel;

namespace Straumr.Core.Configuration;

public static class StraumrKeybindPresets
{
    private static readonly IReadOnlyDictionary<string, string> Empty = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vim"] = Empty,
        ["emacs"] = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["Straumr.OpenCommandPrompt"] = "Alt+x",
            ["Straumr.NextPane"] = "Ctrl+f",
            ["Straumr.PreviousPane"] = "Ctrl+b",
            ["ResourceList.Next"] = "Ctrl+n",
            ["ResourceList.Previous"] = "Ctrl+p",
            ["ResourceList.First"] = "Home",
            ["ResourceList.Last"] = "End",
            ["ResourceList.PageDown"] = "Ctrl+v",
            ["ResourceList.PageUp"] = "PageUp",
            ["ScrollableContent.Next"] = "Ctrl+n",
            ["ScrollableContent.Previous"] = "Ctrl+p",
            ["ScrollableContent.Top"] = "Home",
            ["ScrollableContent.Bottom"] = "End",
            ["ScrollableContent.PageDown"] = "Ctrl+v",
            ["ScrollableContent.PageUp"] = "PageUp",
            ["Select.Next"] = "Ctrl+n",
            ["Select.Previous"] = "Ctrl+p",
            ["Select.First"] = "Home",
            ["Select.Last"] = "End",
            ["Cli.Next"] = "Ctrl+n",
            ["Cli.Previous"] = "Ctrl+p",
            ["Cli.First"] = "Home",
            ["Cli.Last"] = "End",
            ["Cli.Search"] = "Ctrl+s",
            ["ResourceFilter.Open"] = "Ctrl+s",
            ["CommandPrompt.Cancel"] = "Ctrl+g",
            ["ResourceFilter.Cancel"] = "Ctrl+g",
            ["StraumrDialog.Cancel"] = "Ctrl+g",
            ["ConfirmDialog.Cancel"] = "Ctrl+g",
            ["BrowserDialog.Cancel"] = "Ctrl+g",
            ["BrowserDialog.Location.Cancel"] = "Ctrl+g",
            ["SecretSuggestions.Dismiss"] = "Ctrl+g",
            ["Editor.Close"] = "Ctrl+g",
            ["Response.Back"] = "Ctrl+g",
            ["Response.Cancel"] = "Ctrl+g",
            ["Auth.Cancel"] = "Ctrl+g",
            ["Cli.Cancel"] = "Ctrl+g"
        }),

        ["commander"] = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["Straumr.NextPane"] = "Right",
            ["Straumr.NextPaneAlternate"] = "none",
            ["Straumr.PreviousPane"] = "Left",
            ["Straumr.PreviousPaneAlternate"] = "none",
            ["ResourceList.Next"] = "Down",
            ["ResourceList.Previous"] = "Up",
            ["ResourceList.First"] = "Home",
            ["ResourceList.Last"] = "End",
            ["ScrollableContent.Next"] = "Down",
            ["ScrollableContent.Previous"] = "Up",
            ["ScrollableContent.Top"] = "Home",
            ["ScrollableContent.Bottom"] = "End",
            ["Select.Next"] = "Down",
            ["Select.Previous"] = "Up",
            ["Select.First"] = "Home",
            ["Select.Last"] = "End",
            ["Cli.Next"] = "Down",
            ["Cli.Previous"] = "Up",
            ["Cli.First"] = "Home",
            ["Cli.Last"] = "End",
            ["Straumr.OpenCommandPrompt"] = "F9",
            ["Editor.Save"] = "F2",
            ["Editor.SaveAndClose"] = "Shift+F2",
            ["Request.Fullscreen"] = "F3",
            ["Workspace.Edit"] = "F4",
            ["Request.Edit"] = "F4",
            ["Auth.Edit"] = "F4",
            ["Secret.Edit"] = "F4",
            ["Variable.Edit"] = "F4",
            ["KeyValueField.Edit"] = "F4",
            ["ContentField.Edit"] = "F4",
            ["EditorField.Edit"] = "F4",
            ["Workspace.Copy"] = "F5",
            ["Request.Copy"] = "F5",
            ["Request.CopyToWorkspace"] = "Shift+F5",
            ["Auth.Copy"] = "F5",
            ["Auth.CopyToWorkspace"] = "Shift+F5",
            ["Secret.Copy"] = "F5",
            ["Variable.Copy"] = "F5",
            ["Variable.CopyToWorkspace"] = "Shift+F5",
            ["ResponseBody.Copy"] = "F5",
            ["Headers.Copy"] = "F5",
            ["BrowserDialog.Rename"] = "F6",
            ["Workspace.Create"] = "F7",
            ["Request.New"] = "F7",
            ["Auth.New"] = "F7",
            ["Secret.New"] = "F7",
            ["Variable.New"] = "F7",
            ["KeyValueField.Add"] = "F7",
            ["BrowserDialog.New"] = "F7",
            ["BrowserDialog.Rename"] = "F4",
            ["Workspace.Delete"] = "F8",
            ["Request.Delete"] = "F8",
            ["Auth.Delete"] = "F8",
            ["Secret.Delete"] = "F8",
            ["Variable.Delete"] = "F8",
            ["KeyValueField.Remove"] = "F8",
            ["BrowserDialog.Delete"] = "F8",
            ["ResourceFilter.Open"] = "Ctrl+s"
        }),

        ["client"] = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["Straumr.NextPane"] = "Right",
            ["Straumr.NextPaneAlternate"] = "none",
            ["Straumr.PreviousPane"] = "Left",
            ["Straumr.PreviousPaneAlternate"] = "none",
            ["ResourceList.Next"] = "Down",
            ["ResourceList.Previous"] = "Up",
            ["ResourceList.First"] = "Home",
            ["ResourceList.Last"] = "End",
            ["ScrollableContent.Next"] = "Down",
            ["ScrollableContent.Previous"] = "Up",
            ["ScrollableContent.Top"] = "Home",
            ["ScrollableContent.Bottom"] = "End",
            ["Select.Next"] = "Down",
            ["Select.Previous"] = "Up",
            ["Select.First"] = "Home",
            ["Select.Last"] = "End",
            ["Cli.Next"] = "Down",
            ["Cli.Previous"] = "Up",
            ["Cli.First"] = "Home",
            ["Cli.Last"] = "End",
            ["Straumr.OpenCommandPrompt"] = "Ctrl+p",
            ["Request.Send"] = "Ctrl+r",
            ["Auth.Send"] = "Ctrl+r",
            ["Response.Send"] = "Ctrl+r",
            ["Workspace.Create"] = "Ctrl+n",
            ["Request.New"] = "Ctrl+n",
            ["Auth.New"] = "Ctrl+n",
            ["Secret.New"] = "Ctrl+n",
            ["Variable.New"] = "Ctrl+n",
            ["KeyValueField.Add"] = "Ctrl+n",
            ["BrowserDialog.New"] = "Ctrl+n",
            ["Workspace.Copy"] = "Ctrl+d",
            ["Request.Copy"] = "Ctrl+d",
            ["Request.CopyToWorkspace"] = "Ctrl+Shift+d",
            ["Auth.Copy"] = "Ctrl+d",
            ["Auth.CopyToWorkspace"] = "Ctrl+Shift+d",
            ["Secret.Copy"] = "Ctrl+d",
            ["Variable.Copy"] = "Ctrl+d",
            ["Variable.CopyToWorkspace"] = "Ctrl+Shift+d",
            ["Workspace.Delete"] = "Delete",
            ["Request.Delete"] = "Delete",
            ["Auth.Delete"] = "Delete",
            ["Secret.Delete"] = "Delete",
            ["Variable.Delete"] = "Delete",
            ["KeyValueField.Remove"] = "Delete",
            ["BrowserDialog.Delete"] = "Delete",
            ["ResourceFilter.Open"] = "Ctrl+f",
            ["Cli.Search"] = "Ctrl+f"
        })
    };

    public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[] { "vim", "emacs", "commander", "client" });

    public static bool TryGet(string? name, out IReadOnlyDictionary<string, string> bindings)
    {
        if (Presets.TryGetValue(string.IsNullOrWhiteSpace(name) ? "vim" : name.Trim(), out IReadOnlyDictionary<string, string>? found))
        {
            bindings = found;
            return true;
        }
        bindings = Empty;
        return false;
    }

    public static string Hint(string name, string action)
    {
        TryGet(name, out IReadOnlyDictionary<string, string> bindings);
        return bindings.TryGetValue(action, out string? value) ? value : StraumrKeybinds.Defaults[action];
    }

    public static IReadOnlyDictionary<string, string> Resolve(string? name)
    {
        TryGet(name, out IReadOnlyDictionary<string, string> bindings);
        return new ReadOnlyDictionary<string, string>(StraumrKeybinds.Defaults.ToDictionary(
            pair => pair.Key,
            pair => bindings.TryGetValue(pair.Key, out string? value) ? value : pair.Value,
            StringComparer.Ordinal));
    }

    public static string Identify(IReadOnlyDictionary<string, string> keybinds) =>
        Names.FirstOrDefault(name => Presets[name].Count > 0 && Matches(Presets[name], keybinds)) ?? "vim";

    private static bool Matches(IReadOnlyDictionary<string, string> preset,
        IReadOnlyDictionary<string, string> keybinds)
    {
        int matched = 0;
        foreach ((string id, string value) in preset)
        {
            if (!keybinds.TryGetValue(id, out string? bound))
            {
                continue;
            }

            if (!string.Equals(bound?.Trim(), value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            matched++;
        }

        return matched > 0;
    }
}
