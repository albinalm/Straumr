using Spectre.Console;
using Spectre.Console.Rendering;

namespace Straumr.Cli.Console;

public sealed class EscapeCancellableConsole(IAnsiConsole console) : IAnsiConsole
{
    private readonly EscapeCancellableInput _input = new(console.Input);
    public bool WasSearchCancelled => _input.WasSearchCancelled;

    public Profile Profile => console.Profile;
    public IAnsiConsoleCursor Cursor => console.Cursor;
    public IAnsiConsoleInput Input => _input;
    public IExclusivityMode ExclusivityMode => console.ExclusivityMode;
    public RenderPipeline Pipeline => console.Pipeline;

    public void Clear(bool home)
    {
        console.Clear(home);
    }

    public void Write(IRenderable renderable)
    {
        console.Write(renderable);
    }

    public void PrefillInput(string? text)
    {
        _input.Prefill(text);
    }

    public async Task<T> PromptAsync<T>(IPrompt<T> prompt, CancellationToken cancellationToken = default)
    {
        using var cts = new CancellationTokenSource();
        _input.Cts = cts;

        string typeName = prompt.GetType().Name;
        _input.VimMode = typeName.StartsWith("SelectionPrompt") || typeName.StartsWith("MultiSelectionPrompt");
        _input.WasSearchCancelled = false;

        CancellationToken token = cancellationToken == CancellationToken.None
            ? cts.Token
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token).Token;

        int cursorTop = System.Console.CursorTop;
        try
        {
            T result = await AnsiConsoleExtensions.PromptAsync(this, prompt, token);
            System.Console.Out.Flush();
            ClearLines(cursorTop);
            return result;
        }
        catch (OperationCanceledException)
        {
            System.Console.Out.Flush();
            ClearLines(cursorTop);
            throw;
        }
    }

    public static void ClearLines(int fromTop)
    {
        int endTop = System.Console.CursorTop;
        for (int i = fromTop; i <= endTop; i++)
        {
            System.Console.SetCursorPosition(0, i);
            System.Console.Write(new string(' ', System.Console.BufferWidth));
        }

        System.Console.SetCursorPosition(0, fromTop);
    }
}