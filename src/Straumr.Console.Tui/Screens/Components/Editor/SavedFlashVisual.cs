using System.Diagnostics;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Animation;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Layout;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class SavedFlashVisual : Visual, IAnimatedVisual
{
    private readonly Visual _content;
    private readonly State<bool> _flag;
    private long _until;

    public SavedFlashVisual(Visual content, State<bool> flag)
    {
        _content = content;
        _flag = flag;
        AttachChild(content);
    }

    protected override int ChildrenCount => 1;

    public long NextAnimationTick => _flag.Value ? _until : long.MaxValue;

    public bool AdvanceAnimation(long timestamp)
    {
        if (!_flag.Value || timestamp < _until)
        {
            return false;
        }

        _flag.Value = false;
        return true;
    }

    public void Arm(TimeSpan lifetime) =>
        _until = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * lifetime.TotalSeconds);

    protected override Visual GetChild(int index) =>
        index == 0 ? _content : throw new ArgumentOutOfRangeException(nameof(index));

    protected override SizeHints MeasureCore(in LayoutConstraints constraints) =>
        _content.Measure(constraints);

    protected override void ArrangeCore(in Rectangle finalRect) => _content.Arrange(finalRect);
}
