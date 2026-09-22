namespace Straumr.Console.Shared.Interfaces;

public interface IInteractiveConsole
{
    string? Select(
        string title,
        IReadOnlyList<string> choices,
        Func<string, string>? displayConverter = null,
        bool enableFilter = true,
        bool enableTypeahead = false);
    string? TextInput(string title, string? initialValue = null, bool allowEmpty = false, Func<string, string?>? validate = null);
    string? SecretInput(string title);
    void ShowMessage(string title, string message);
    void ShowMessage(string message);
    void ShowTable(string col1, string col2, IEnumerable<(string Key, string Value)> rows, string emptyMessage);

    bool TryEditKeyValuePairs(string title, IDictionary<string, string> items, Action? onSaved = null) => false;
}
