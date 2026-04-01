using Spectre.Console;

namespace Straumr.Cli.Console;

public sealed class EscapeCancellableInput(IAnsiConsoleInput originalInput) : IAnsiConsoleInput
{
    public CancellationTokenSource? Cts { get; set; }
    public bool VimMode { get; set; }
    public bool SearchActive { get; set; }
    public bool WasSearchCancelled { get; set; }

    public bool IsKeyAvailable() => originalInput.IsKeyAvailable();

    public ConsoleKeyInfo? ReadKey(bool intercept)
    {
        while (true)
        {
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
}
