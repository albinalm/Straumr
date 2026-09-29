using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class ReferencesView
{
    private readonly ResourceList _list;
    private readonly State<string> _message = new("Unavailable.");
    private ReferenceModel[] _references = [];
    public ReferencesView(Action<ReferenceModel> activate)
    {
        _list = new ResourceList([], new TextBlock(() => _message.Value)
            .Style(StraumrStyleService.MutedText).Wrap(true), "Go to");
        _list.ItemActivated += index => activate(_references[index]);
        Root = ResourceScreenLayoutHelpers.Scrollable(_list);
    }
    public Visual Root { get; }
    public Visual FocusTarget => _list;
    public void SetReferences(IReadOnlyList<ReferenceModel> references, bool resetSelection = false)
    {
        ReferenceModel? selected = !resetSelection && (uint)_list.SelectedIndex < (uint)_references.Length
            ? _references[_list.SelectedIndex] : null;
        _references = references.OrderBy(reference => reference.IsSecret)
            .ThenBy(reference => reference.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        _message.Value = "No variable or secret references.";
        _list.SetRows(_references.Select(reference => new ResourceRowModel(reference.Name,
            $"{(reference.IsSecret ? "secret" : "variable")} · {(reference.Available ? "available" : "unavailable")}",
            IsBroken: !reference.Available)));
        int index = selected is null ? -1 : Array.FindIndex(_references, reference =>
            reference.IsSecret == selected.IsSecret && reference.Name.Equals(selected.Name, StringComparison.Ordinal));
        _list.SelectedIndex = index >= 0 ? index : 0;
    }
    public void SetMessage(string message)
    {
        _message.Value = message;
        _list.SetRows([]);
    }
}
