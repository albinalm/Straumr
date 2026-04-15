using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Infrastructure;

internal sealed class KeyDiagnostics
{
    private const int Capacity = 12;

    private readonly string[] _lines = new string[Capacity];
    private int _count;
    private Label? _label;
    private bool _visible;

    public void Toggle()
    {
        _visible = !_visible;
        if (_label is not null)
        {
            _label.Visible = _visible;
            _label.Text = Render();
            _label.SetNeedsDraw();
        }
    }

    public void Record(string line)
    {
        Push(line);
        if (_visible && _label is not null)
        {
            _label.Text = Render();
            _label.SetNeedsDraw();
        }
    }

    public void AttachTo(View host)
    {
        if (ReferenceEquals(_label?.SuperView, host))
        {
            _label.Visible = _visible;
            _label.Text = Render();
            _label.SetNeedsDraw();
            return;
        }

        if (_label?.SuperView is { } previousHost)
        {
            try
            {
                previousHost.Remove(_label);
            }
            catch
            {
                // Ignore disposal/removal races when switching between modal windows.
            }
        }

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
        var sb = new System.Text.StringBuilder();
        sb.Append("F12 key diag (last ").Append(Capacity).Append(')');
        for (var i = 0; i < _count; i++)
        {
            sb.Append('\n').Append(_lines[i]);
        }

        return sb.ToString();
    }
}
