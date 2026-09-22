namespace Straumr.Console.Tui.Formatting;

internal static class TimestampFormatting
{
    public static string Relative(DateTimeOffset value)
    {
        DateTime local = value.LocalDateTime;
        int days = (DateTime.Today - local.Date).Days;

        return days switch
        {
            < 0 => local.ToString("yyyy-MM-dd HH:mm"),
            0 => $"today {local:HH:mm}",
            1 => $"yesterday {local:HH:mm}",
            < 7 => $"{days} days ago",
            _ => local.ToString("yyyy-MM-dd HH:mm")
        };
    }

    public static string Absolute(DateTimeOffset value) =>
        value.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
}
