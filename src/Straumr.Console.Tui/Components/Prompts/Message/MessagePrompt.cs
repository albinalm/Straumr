using Straumr.Console.Tui.Components.Prompts.Base;
using Straumr.Console.Tui.Components.TextFields;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;
using MarkupText = Straumr.Console.Tui.Helpers.MarkupText;

namespace Straumr.Console.Tui.Components.Prompts.Message;

internal sealed class MessagePrompt : PromptComponent
{
    public required string Title { get; init; }
    public required string Message { get; init; }

    public override View Build()
    {
        View stack = new()
        {
            X = Pos.Center(),
            Y = Pos.Percent(50) - 3,
            Width = Dim.Percent(60),
            Height = 9,
        };

        Label titleLabel = new()
        {
            Text = Title,
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            TextAlignment = Alignment.Center,
            CanFocus = false,
        };
        ApplyLabelTheme(titleLabel);

        InteractiveTextView messageView = new()
        {
            Text = MarkupText.ToPlain(Message),
            ReadOnly = true,
            CanFocus = false,
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = 4,
            WordWrap = true,
        };
        messageView.TextFormatter.Alignment = Alignment.Center;
        ApplyTextTheme(messageView);

        Button continueButton = new()
        {
            Text = "Press any key to continue",
            X = Pos.Center(),
            Y = 7,
            Width = Dim.Auto(),
            CanFocus = false,
        };
        Scheme? buttonScheme = BuildButtonScheme();
        if (buttonScheme is not null)
        {
            continueButton.SetScheme(buttonScheme);
        }

        stack.Add(titleLabel, messageView, continueButton);
        return stack;
    }

    private void ApplyTextTheme(InteractiveTextView text)
    {
        if (Theme is null)
        {
            return;
        }

        var background = ColorResolver.Resolve(Theme.Surface);
        var foreground = ColorResolver.Resolve(Theme.OnSurface);
        text.ApplyTheme(background, foreground);
    }

    private void ApplyLabelTheme(Label label)
    {
        if (Theme is null)
        {
            return;
        }

        var attr = new Attribute(ColorResolver.Resolve(Theme.OnSurface), ColorResolver.Resolve(Theme.Surface));
        label.SetScheme(new Scheme(attr)
        {
            Normal = new Attribute(attr),
            Focus = new Attribute(attr),
            Disabled = new Attribute(attr),
        });
    }
}
