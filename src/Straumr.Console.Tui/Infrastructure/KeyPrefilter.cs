using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Straumr.Console.Tui.Infrastructure;

internal sealed class KeyPrefilter : IDisposable
{
    private const KeyCode ModifierMask = KeyCode.ShiftMask | KeyCode.CtrlMask | KeyCode.AltMask;

    private readonly IApplication _application;
    private readonly KeyDiagnostics _diagnostics;
    private readonly Stack<HandlerRegistration> _handlers = new();

    public KeyPrefilter(IApplication application, KeyDiagnostics diagnostics)
    {
        _application = application;
        _diagnostics = diagnostics;
        _application.Keyboard.KeyDown += OnKeyDown;
    }

    public void Push(string name, Func<Key, bool> handler)
    {
        _handlers.Push(new HandlerRegistration(name, handler));
        RecordEvent($"PUSH {name}");
    }

    public void Pop()
    {
        if (_handlers.Count == 0)
        {
            return;
        }

        HandlerRegistration popped = _handlers.Pop();
        RecordEvent($"POP {popped.Name}");
    }

    public void RecordEvent(string message)
        => _diagnostics.Record($"EVT {message} | S={_handlers.Count} N={CurrentHandlerName()} T={DescribeView(_application.TopRunnableView)} F={DescribeView(_application.TopRunnableView?.MostFocused)}");

    private void OnKeyDown(object? sender, Key key)
    {
        if (key.KeyCode == KeyCode.F12)
        {
            _diagnostics.Toggle();
            key.Handled = true;
            return;
        }

        KeyCode kc = key.KeyCode;
        int rune = key.AsRune.Value;
        KeyCode sk = key.ShiftedKeyCode;
        KeyCode bk = key.BaseLayoutKeyCode;
        string mods = Mods(key);

        if (IsModifierOnly(kc))
        {
            key.Handled = true;
            _diagnostics.Record(Format(
                kc,
                rune,
                sk,
                bk,
                mods,
                _handlers.Count,
                CurrentHandlerName(),
                DescribeView(_application.TopRunnableView),
                DescribeView(_application.TopRunnableView?.MostFocused),
                handled: false,
                modOnly: true));
            return;
        }

        HandlerRegistration? current = _handlers.Count > 0 ? _handlers.Peek() : null;
        bool handled = current is not null && current.Handler(key);
        if (handled)
        {
            key.Handled = true;
        }

        _diagnostics.Record(Format(
            kc,
            rune,
            sk,
            bk,
            mods,
            _handlers.Count,
            current?.Name ?? "-",
            DescribeView(_application.TopRunnableView),
            DescribeView(_application.TopRunnableView?.MostFocused),
            handled,
            modOnly: false));
    }

    private static bool IsModifierOnly(KeyCode keyCode)
    {
        return (keyCode & ~ModifierMask) == KeyCode.Null;
    }

    private static string Format(
        KeyCode kc,
        int rune,
        KeyCode sk,
        KeyCode bk,
        string mods,
        int stack,
        string handlerName,
        string topView,
        string focusedView,
        bool handled,
        bool modOnly)
    {
        var sb = new StringBuilder();
        sb.Append("KC=").Append(kc);
        sb.Append(" R=0x").Append(rune.ToString("X"));
        sb.Append(" SK=").Append(sk);
        sb.Append(" BK=").Append(bk);
        sb.Append(" M=").Append(mods);
        sb.Append(" | S=").Append(stack);
        sb.Append(" N=").Append(handlerName);
        sb.Append(" T=").Append(topView);
        sb.Append(" F=").Append(focusedView);
        sb.Append(" H=").Append(handled ? 'Y' : 'N');
        if (modOnly)
        {
            sb.Append(" [modOnly]");
        }

        return sb.ToString();
    }

    private static string Mods(Key key)
    {
        var sb = new StringBuilder(3);
        if (key.IsCtrl)
        {
            sb.Append('C');
        }

        if (key.IsAlt)
        {
            sb.Append('A');
        }

        if (key.IsShift)
        {
            sb.Append('S');
        }

        return sb.Length == 0 ? "-" : sb.ToString();
    }

    private string CurrentHandlerName() => _handlers.Count > 0 ? _handlers.Peek().Name : "-";

    private static string DescribeView(View? view)
    {
        if (view is null)
        {
            return "-";
        }

        var sb = new StringBuilder(view.GetType().Name);
        string? id = view.Id?.ToString();
        string? title = view.Title?.ToString();

        if (!string.IsNullOrWhiteSpace(id))
        {
            sb.Append('#').Append(id);
        }
        else if (!string.IsNullOrWhiteSpace(title))
        {
            sb.Append('[').Append(Truncate(title, 16)).Append(']');
        }

        return sb.ToString();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 3)] + "...";
    }

    private sealed record HandlerRegistration(string Name, Func<Key, bool> Handler);

    public void Dispose() => _application.Keyboard.KeyDown -= OnKeyDown;
}
