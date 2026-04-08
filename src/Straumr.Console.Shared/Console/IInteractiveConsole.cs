namespace Straumr.Console.Shared.Console;

public interface IInteractiveConsole
{
    Task<string?> SelectAsync(string title, IReadOnlyList<string> choices, Func<string, string>? displayConverter = null);
    Task<string?> TextInputAsync(string title, string? initialValue = null, bool allowEmpty = false, Func<string, string?>? validate = null);
    Task<string?> SecretInputAsync(string title);
    void ShowMessage(string message);
    void ShowTable(string col1, string col2, IEnumerable<(string Key, string Value)> rows, string emptyMessage);
}
