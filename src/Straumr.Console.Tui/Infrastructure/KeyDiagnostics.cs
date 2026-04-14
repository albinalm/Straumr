using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Infrastructure;

internal sealed class KeyDiagnostics : IDisposable
{
    private const int Capacity = 8;

    private readonly IApplication _application;
    private readonly string[] _lines = new string[Capacity];
    private int _count;
    private Label? _label;
    private bool _visible;

    public KeyDiagnostics(IApplication application)
    {
        _application = application;
        _application.Keyboard.KeyDown += OnKeyDown;
    }

    public void AttachTo(View host)
    {
        _label = new Label
        {
            X = 0,
            Y = Pos.AnchorEnd(Capacity + 1),
            Width = Dim.Fill(),
            Height = Capacity + 1,
            Visible = _visible,
            Text = Render(),
        };
        host.Add(_label);
    }

    private void OnKeyDown(object? sender, Key key)
    {
        if (key.KeyCode == KeyCode.F12)
        {
            _visible = !_visible;
            if (_label is not null)
            {
                _label.Visible = _visible;
                _label.SetNeedsDraw();
            }

            key.Handled = true;
            return;
        }

        Push(Format(key));
        if (_visible && _label is not null)
        {
            _label.Text = Render();
            _label.SetNeedsDraw();
        }
    }

    private void Push(string line)
    {
        if (_count < Capacity)
        {
            _lines[_count++] = line;
            return;
        }

        for (var i = 1; i < Capacity; i++)
        {
            _lines[i - 1] = _lines[i];
        }

        _lines[Capacity - 1] = line;
    }

    private string Render()
    {
        var sb = new StringBuilder();
        sb.Append("F12 key diag (last ").Append(Capacity).Append(')');
        for (var i = 0; i < _count; i++)
        {
            sb.Append('\n').Append(_lines[i]);
        }

        return sb.ToString();
    }

    private static string Format(Key key)
    {
        return $"KC={key.KeyCode} R=0x{key.AsRune.Value:X} SK={key.ShiftedKeyCode} BK={key.BaseLayoutKeyCode} M={Mods(key)}";
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

    public void Dispose() => _application.Keyboard.KeyDown -= OnKeyDown;
}
