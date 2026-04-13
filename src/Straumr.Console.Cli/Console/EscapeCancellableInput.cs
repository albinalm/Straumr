using Spectre.Console;

namespace Straumr.Console.Cli.Console;

public sealed class EscapeCancellableInput(IAnsiConsoleInput originalInput) : IAnsiConsoleInput
{
    private readonly Queue<ConsoleKeyInfo> _prefill = new();
    public CancellationTokenSource? Cts { get; set; }
    public bool VimMode { get; set; }
    public bool SearchActive { get; set; }
    public bool WasSearchCancelled { get; set; }

    public bool IsKeyAvailable()
    {
        return originalInput.IsKeyAvailable();
    }

    public ConsoleKeyInfo? ReadKey(bool intercept)
    {
        while (true)
        {
            if (_prefill.Count > 0)
            {
                return _prefill.Dequeue();
            }

            ConsoleKeyInfo? key = originalInput.ReadKey(intercept);
            if (key is null)
            {
                return null;
            }

            ConsoleKeyInfo? translated = TranslateKey(key.Value);
            if (translated is not null)
            {
                return translated;
            }
        }
    }

    public async Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
    {
        while (true)
        {
            if (_prefill.Count > 0)
            {
                return _prefill.Dequeue();
            }

            ConsoleKeyInfo? key = await originalInput.ReadKeyAsync(intercept, cancellationToken);
            if (key is null)
            {
                return null;
            }

            ConsoleKeyInfo? translated = TranslateKey(key.Value);
            if (translated is not null)
            {
                return translated;
            }
        }
    }

    private ConsoleKeyInfo? TranslateKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            if (VimMode && SearchActive)
            {
                SearchActive = false;
                WasSearchCancelled = true;
                Cts?.Cancel();
                return key;
            }

            WasSearchCancelled = false;
            Cts?.Cancel();
            return key;
        }

        if (!VimMode)
        {
            return key;
        }

        if (SearchActive)
        {
            return key.KeyChar is 'j' or 'k' ? new ConsoleKeyInfo(key.KeyChar, 0, false, false, false) : key;
        }

        return key.KeyChar switch
        {
            'j' or 'J' => new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false),
            'k' or 'K' => new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false),
            'g' => new ConsoleKeyInfo('\0', ConsoleKey.Home, false, false, false),
            'G' => new ConsoleKeyInfo('\0', ConsoleKey.End, false, false, false),
            '/' => Activate(),
            _ => char.IsControl(key.KeyChar) ? key : null
        };
    }

    private ConsoleKeyInfo? Activate()
    {
        SearchActive = true;
        return null;
    }

    public void Prefill(string? text)
    {
        _prefill.Clear();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (char ch in text) _prefill.Enqueue(new ConsoleKeyInfo(ch, GuessConsoleKey(ch), false, false, false));
    }

    private static ConsoleKey GuessConsoleKey(char ch)
    {
        if (char.IsLetter(ch))
        {
            string name = char.ToUpperInvariant(ch).ToString();
            if (Enum.TryParse(name, out ConsoleKey key))
            {
                return key;
            }
        }

        if (char.IsDigit(ch))
        {
            if (Enum.TryParse("D" + ch, out ConsoleKey key))
            {
                return key;
            }
        }

        return ConsoleKey.NoName;
    }
}