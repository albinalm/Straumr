using System.Net.NetworkInformation;

namespace Straumr.Console.Tui.Services;

internal static class NetworkStatsService
{
    private const int TrafficHistory = 10;
    private const long IdleMilliseconds = 5000;
    private static readonly Lock Gate = new();
    private static readonly Queue<double> Downs = new();
    private static readonly Queue<double> Ups = new();
    private static Timer? _timer;
    private static bool _unavailable;
    private static long _lastRead;
    private static long _lastSample;
    private static long _receivedTotal = -1;
    private static long _sentTotal = -1;
    private static double _down;
    private static double _up;

    public static NetworkStatsModel Snapshot()
    {
        lock (Gate)
        {
            _lastRead = Environment.TickCount64;
            if (_timer is null && !_unavailable)
            {
                _timer = new Timer(Sample, null, 0, 1000);
            }

            return new NetworkStatsModel(!_unavailable && Downs.Count > 0, _down, _up, Downs.ToArray(), Ups.ToArray());
        }
    }

    private static void Sample(object? state)
    {
        long now = Environment.TickCount64;
        lock (Gate)
        {
            if (now - _lastRead > IdleMilliseconds)
            {
                _receivedTotal = -1;
                return;
            }
        }

        if (!TryReadTotals(out long received, out long sent))
        {
            lock (Gate)
            {
                _unavailable = true;
                _timer?.Dispose();
                _timer = null;
            }

            return;
        }

        lock (Gate)
        {
            if (_receivedTotal >= 0 && now > _lastSample)
            {
                double seconds = (now - _lastSample) / 1000.0;
                _down = Math.Max(0, (received - _receivedTotal) / seconds);
                _up = Math.Max(0, (sent - _sentTotal) / seconds);
                Push(Downs, _down, TrafficHistory);
                Push(Ups, _up, TrafficHistory);
            }

            _receivedTotal = received;
            _sentTotal = sent;
            _lastSample = now;
        }
    }

    private static bool TryReadTotals(out long received, out long sent)
    {
        received = 0;
        sent = 0;
        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (Exception exception) when (exception is NetworkInformationException or PlatformNotSupportedException)
        {
            return false;
        }

        bool any = false;
        foreach (NetworkInterface candidate in interfaces)
        {
            if (candidate.OperationalStatus != OperationalStatus.Up ||
                candidate.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            try
            {
                IPInterfaceStatistics statistics = candidate.GetIPStatistics();
                received += statistics.BytesReceived;
                sent += statistics.BytesSent;
                any = true;
            }
            catch (Exception exception) when (exception is NetworkInformationException or PlatformNotSupportedException)
            {
            }
        }

        return any;
    }

    private static void Push(Queue<double> history, double value, int limit)
    {
        history.Enqueue(value);
        while (history.Count > limit)
        {
            history.Dequeue();
        }
    }
}
