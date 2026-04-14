using System;
using System.Collections.Generic;
using System;
using System.Collections.Generic;
using Straumr.Console.Tui.Components.Prompts.Base;
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
        const int wrapWidth = 60;
        string wrappedText = WrapMessage(MarkupText.ToPlain(Message), wrapWidth);

        View stack = new()
        {
            X = Pos.Center(),
            Y = Pos.Percent(50) - 3,
            Width = Dim.Percent(60),
            Height = Dim.Auto(),
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

        Label messageLabel = new()
        {
            Text = wrappedText,
            CanFocus = false,
            X = 0,
            Y = Pos.Bottom(titleLabel) + 1,
            Width = Dim.Fill(),
            Height = Dim.Auto(),
            TextAlignment = Alignment.Center,
        };
        ApplyLabelTheme(messageLabel);

        Button continueButton = new()
        {
            Text = "Press any key to continue",
            X = Pos.Center(),
            Y = Pos.Bottom(messageLabel) + 1,
            Width = Dim.Auto(),
            CanFocus = false,
            ShadowStyle = ShadowStyle.None
        };
        Scheme? buttonScheme = BuildButtonScheme();
        if (buttonScheme is not null)
        {
            continueButton.SetScheme(buttonScheme);
        }

        stack.Add(titleLabel, messageLabel, continueButton);
        return stack;
    }

    private static string WrapMessage(string text, int maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text) || maxWidth <= 0)
        {
            return text ?? string.Empty;
        }

        var lines = new List<string>();

        foreach (string raw in text.Split('\n'))
        {
            if (raw.Length <= maxWidth)
            {
                lines.Add(raw);
                continue;
            }

            int pos = 0;
            while (pos < raw.Length)
            {
                int remaining = raw.Length - pos;
                if (remaining <= maxWidth)
                {
                    lines.Add(raw[pos..]);
                    break;
                }

                int breakAt = raw.LastIndexOf(" ", Math.Min(raw.Length - 1, pos + maxWidth - 1), Math.Min(maxWidth, raw.Length - pos), StringComparison.Ordinal);
                if (breakAt <= pos)
                {
                    breakAt = pos + maxWidth;
                }

                lines.Add(raw[pos..breakAt].TrimEnd());
                pos = breakAt;
                while (pos < raw.Length && raw[pos] == ' ')
                {
                    pos++;
                }
            }
        }

        return string.Join('\n', lines);
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
