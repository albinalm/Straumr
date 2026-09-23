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
            ["Straumr.OpenCommandPrompt"] = "Alt+X",
            ["ResourceList.Next"] = "Ctrl+N",
            ["ResourceList.Previous"] = "Ctrl+P",
            ["ResourceList.First"] = "Home",
            ["ResourceList.Last"] = "End",
            ["ResourceList.PageDown"] = "Ctrl+V",
            ["ResourceList.PageUp"] = "PageUp",
            ["ScrollableContent.Next"] = "Ctrl+N",
            ["ScrollableContent.Previous"] = "Ctrl+P",
            ["ScrollableContent.Top"] = "Home",
            ["ScrollableContent.Bottom"] = "End",
            ["ScrollableContent.PageDown"] = "Ctrl+V",
            ["ScrollableContent.PageUp"] = "PageUp",
            ["Select.Next"] = "Ctrl+N",
            ["Select.Previous"] = "Ctrl+P",
            ["Select.First"] = "Home",
            ["Select.Last"] = "End",
            ["Cli.Next"] = "Ctrl+N",
            ["Cli.Previous"] = "Ctrl+P",
            ["Cli.First"] = "Home",
            ["Cli.Last"] = "End",
            ["Cli.Search"] = "Ctrl+S",
            ["ResourceFilter.Open"] = "Ctrl+S",
            ["CommandPrompt.Cancel"] = "Ctrl+G",
            ["ResourceFilter.Cancel"] = "Ctrl+G",
            ["StraumrDialog.Cancel"] = "Ctrl+G",
            ["ConfirmDialog.Cancel"] = "Ctrl+G",
            ["BrowserDialog.Cancel"] = "Ctrl+G",
            ["BrowserDialog.Location.Cancel"] = "Ctrl+G",
            ["SecretSuggestions.Dismiss"] = "Ctrl+G",
            ["Editor.Close"] = "Ctrl+G",
            ["Response.Back"] = "Ctrl+G",
            ["Response.Cancel"] = "Ctrl+G",
            ["Auth.Cancel"] = "Ctrl+G",
            ["Cli.Cancel"] = "Ctrl+G"
        }),

        ["commander"] = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["Straumr.OpenCommandPrompt"] = "F9",
            ["Editor.Save"] = "F2",
            ["Request.Fullscreen"] = "F3",
            ["Workspace.Edit"] = "F4",
            ["Request.Edit"] = "F4",
            ["Auth.Edit"] = "F4",
            ["Secret.Edit"] = "F4",
            ["Variable.Edit"] = "F4",
            ["KeyValueField.Edit"] = "F4",
            ["ContentField.Edit"] = "F4",
            ["Workspace.Copy"] = "F5",
            ["Request.Copy"] = "F5",
            ["Auth.Copy"] = "F5",
            ["Secret.Copy"] = "F5",
            ["Variable.Copy"] = "F5",
            ["ResponseBody.Copy"] = "F5",
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
            ["ResourceFilter.Open"] = "Ctrl+S"
        }),

        ["client"] = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["Straumr.OpenCommandPrompt"] = "Ctrl+P",
            ["Request.Send"] = "Ctrl+R",
            ["Response.Send"] = "Ctrl+R",
            ["Workspace.Create"] = "Ctrl+N",
            ["Request.New"] = "Ctrl+N",
            ["Auth.New"] = "Ctrl+N",
            ["Secret.New"] = "Ctrl+N",
            ["Variable.New"] = "Ctrl+N",
            ["KeyValueField.Add"] = "Ctrl+N",
            ["BrowserDialog.New"] = "Ctrl+N",
            ["Workspace.Copy"] = "Ctrl+D",
            ["Request.Copy"] = "Ctrl+D",
            ["Auth.Copy"] = "Ctrl+D",
            ["Secret.Copy"] = "Ctrl+D",
            ["Variable.Copy"] = "Ctrl+D",
            ["Workspace.Delete"] = "Delete",
            ["Request.Delete"] = "Delete",
            ["Auth.Delete"] = "Delete",
            ["Secret.Delete"] = "Delete",
            ["Variable.Delete"] = "Delete",
            ["KeyValueField.Remove"] = "Delete",
            ["BrowserDialog.Delete"] = "Delete",
            ["ResourceFilter.Open"] = "Ctrl+F",
            ["Cli.Search"] = "Ctrl+F"
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

    public static bool TryNormalizeExpanded(IReadOnlyDictionary<string, string> keybinds,
        out string preset, out IReadOnlyDictionary<string, string> neutralized)
    {
        preset = "vim";
        neutralized = Empty;
        if (keybinds.Count < StraumrKeybinds.Defaults.Count * 3 / 4)
        {
            return false;
        }

        int best = 0;
        Dictionary<string, string> matches = new(StringComparer.Ordinal);
        foreach (string name in Names)
        {
            IReadOnlyDictionary<string, string> baseline = Resolve(name);
            Dictionary<string, string> candidate = new(StringComparer.Ordinal);
            foreach ((string id, string value) in keybinds)
            {
                string? legacy = Legacy(name, id);
                if (baseline.TryGetValue(id, out string? expected) &&
                    (string.Equals(value, expected, StringComparison.Ordinal) ||
                     legacy is not null && string.Equals(value, legacy, StringComparison.Ordinal)))
                {
                    candidate[id] = "default";
                }
            }

            if (candidate.Count <= best)
            {
                continue;
            }

            best = candidate.Count;
            preset = name;
            matches = candidate;
        }

        if (best < StraumrKeybinds.Defaults.Count * 3 / 4)
        {
            return false;
        }

        neutralized = matches;
        return true;
    }

    private static string? Legacy(string preset, string action) => action switch
    {
        "ContentField.Edit" => "Enter",
        "KeyValueField.Add" when preset != "commander" => "a",
        "BrowserDialog.New" when preset != "commander" => "n",
        "BrowserDialog.Rename" => preset == "commander" ? "F6" : "r",
        _ => null
    };

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
