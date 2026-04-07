using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Straumr.Cli.Theme;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Command = Spectre.Console.Cli.Command;
using TuiAttribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Cli.Commands.Tui;

public sealed class TuiCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    StraumrThemeOptions theme) : Command
{
    private const string Figlet = """
                                       _
                                   ___| |_ _ __ __ _ _   _ _ __ ___  _ __
                                  / __| __| '__/ _` | | | | '_ ` _ \| '__|
                                  \__ \ |_| | | (_| | |_| | | | | | | |
                                  |___/\__|_|  \__,_|\__,_|_| |_| |_|_|

                                  """;

    [UnconditionalSuppressMessage("AOT",
        "IL2026:Using member 'Terminal.Gui.App.IApplication.Init(String)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code.",
        Justification = "TUI mode is a lightweight UI test surface; trimming impact is acceptable.")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Using member 'Terminal.Gui.App.IApplication.Init(String)' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling.",
        Justification = "TUI mode is a lightweight UI test surface; dynamic code use is acceptable.")]
    public override int Execute(Spectre.Console.Cli.CommandContext context, CancellationToken cancellationToken)
    {
        using IApplication app = Application.Create();
        app.Init();

        StraumrTuiTheme t = theme.Tui;
        var scheme = new Scheme(new TuiAttribute(ParseColor(t.Foreground), ParseColor(t.Background)))
        {
            Focus = new TuiAttribute(ParseColor(t.Accent), ParseColor(t.SelectionBackground)),
            HotNormal = new TuiAttribute(ParseColor(t.Accent), ParseColor(t.Background)),
            HotFocus = new TuiAttribute(ParseColor(t.Background), ParseColor(t.Accent)),
            Disabled = new TuiAttribute(ParseColor(t.Muted), ParseColor(t.Background)),
        };

        using Window window = new();
        window.Width = Dim.Fill();
        window.Height = Dim.Fill();
        window.BorderStyle = LineStyle.None;
        window.SetScheme(scheme);

        Action requestStop = app.RequestStop;
        window.KeyDown += (_, key) =>
        {
            if (key == Key.Esc)
            {
                requestStop();
                key.Handled = true;
            }
        };

        int figletWidth = Figlet.Split('\n').Max(l => l.Length);
        int figletHeight = Figlet.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        Label banner = new()
        {
            Text = Figlet,
            X = Pos.AnchorEnd(figletWidth + 1),
            Y = 0,
        };

        FrameView workspaceFrame = new()
        {
            Title = "Workspaces",
            X = 1,
            Y = figletHeight + 1,
            Width = Dim.Fill(2),
            Height = Dim.Fill(1),
        };

        var items = new ObservableCollection<string>(LoadWorkspaceLines());
        ListView workspaceList = new()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Source = new ListWrapper<string>(items),
        };

        workspaceFrame.Add(workspaceList);
        window.Add(banner, workspaceFrame);

        app.Run(window);

        return 0;
    }

    private static Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        var r = Convert.ToInt32(hex[0..2], 16);
        var g = Convert.ToInt32(hex[2..4], 16);
        var b = Convert.ToInt32(hex[4..6], 16);
        return new Color(r, g, b);
    }

    private List<string> LoadWorkspaceLines()
    {
        if (optionsService.Options.Workspaces.Count == 0)
        {
            return ["No workspaces found."];
        }

        List<WorkspaceLine> items = optionsService.Options.Workspaces.Select(BuildWorkspaceLine).ToList();

        return items
            .OrderByDescending(item => item.LastAccessed)
            .Select(item => item.Display)
            .ToList();
    }

    private WorkspaceLine BuildWorkspaceLine(StraumrWorkspaceEntry entry)
    {
        var name = "Unknown";
        string status;
        DateTimeOffset? lastAccessed = null;

        try
        {
            StraumrWorkspace workspace = workspaceService.PeekWorkspace(entry.Path).GetAwaiter().GetResult();
            name = workspace.Name;
            status = "Valid";
            lastAccessed = workspace.LastAccessed;
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            status = "Corrupt";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.EntryNotFound)
        {
            status = "Missing";
        }

        bool isCurrent = optionsService.Options.CurrentWorkspace?.Id == entry.Id;
        string idShort = entry.Id.ToString("N")[..8];
        string marker = isCurrent ? "* " : "  ";
        var display = $"{marker}{name}  [{idShort}]  {status}";

        return new WorkspaceLine(display, lastAccessed);
    }

    private sealed record WorkspaceLine(string Display, DateTimeOffset? LastAccessed);
}