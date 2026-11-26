namespace Straumr.Console.Tui.Models;

internal sealed record NetworkStatsModel(
    bool HasTraffic,
    double Down,
    double Up,
    IReadOnlyList<double> DownHistory,
    IReadOnlyList<double> UpHistory);
