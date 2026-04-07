namespace Straumr.Cli.Theme;

public class StraumrThemeOptions
{
    public StraumrTuiTheme Tui { get; set; } = new();
}

public class StraumrTuiTheme
{
    public string Background { get; set; } = "#1a1b26";
    public string Foreground { get; set; } = "#a9b1d6";
    public string Accent { get; set; } = "#7aa2f7";
    public string SelectionBackground { get; set; } = "#283457";
    public string Muted { get; set; } = "#565f89";
}
