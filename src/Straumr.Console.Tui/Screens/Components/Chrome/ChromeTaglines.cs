using System.Diagnostics;

namespace Straumr.Console.Tui.Screens.Components.Chrome;

internal static class ChromeTaglines
{
    private static readonly string[] Anytime =
    [
        "It's not DNS. It's never DNS. It was DNS",
        "There's no place like 127.0.0.1",
        "I'd tell you a UDP joke, but you might not get it",
        "I'd tell you a TCP joke, but you'd have to ACK it",
        "SYN, SYN-ACK, ACK, coffee",
        "418: I'm a teapot, and so is prod",
        "Works on my localhost",
        "Retry-After: fika",
        "Content-Type: application/fika",
        "CORS? Of course",
        "Ping me maybe",
        "Packet loss is just packets finding themselves",
        "A watched request never returns",
        "Status 200, mood 200",
        "Cache me outside",
        "Bearer of good tokens",
        "Idempotent and at peace",
        "Handshake first, questions later",
        "Keep calm and carry headers",
        "The cloud is just someone else's river",
        "Go with the flow control",
        "Stream responsibly",
        "Upstream issues, downstream tears",
        "Every packet finds the sea",
        "Keep your headers above water",
        "Row, row, row your POST",
        "Fishing for 200s",
        "Redirects are just rapids",
        "Timeouts are just low tide",
        "Something smells phishy",
        "Water under the bridge, JSON under the hood",
        "Like a stream, but with headers",
        "Stay hydrated, stay idempotent",
        "Latency is just the scenic route",
        "Make waves, not 500s",
        "Keep-alive, stay afloat",
        "Drop packets, not friends",
        "Sink or stream",
        "Throttled? Paddle slower",
        "No 5xx in these waters",
        "Have you tried turning it off and on again?",
        "Null and void, mostly null",
        "It's always a timezone",
        "Off by one, on by default",
        "Ship it, then fix it",
        "Your request is important to us",
        "Have a 200 day",
        "Exponential backoff, linear coffee",
        "There are 10 kinds of requests",
        "Let it flow, let it 200"
    ];

    private static readonly string[] Morning =
    [
        "Hope the coffee kicked in",
        "Morning tide, fresh tokens",
        "Rise and stream",
        "First request of the day, make it a GET"
    ];

    private static readonly string[] Afternoon =
    [
        "Afternoon current is steady",
        "Fika first, then deploy",
        "Post-lunch POSTs"
    ];

    private static readonly string[] Evening =
    [
        "One more request, then home",
        "Sunset over the load balancer",
        "Deploying on a Friday evening?"
    ];

    private static readonly string[] Night =
    [
        "Night owls send quiet requests",
        "The servers are dreaming of streams",
        "Late night, low latency"
    ];

    private static string? _current;
    private static DateTime _next;
    private static long _since;

    public static (string Line, long Since) Current
    {
        get
        {
            DateTime now = DateTime.Now;
            if (_current is not { } current || now >= _next)
            {
                string[] pool = Random.Shared.Next(4) == 0 ? ForHour(now.Hour) : Anytime;
                string picked = pool[Random.Shared.Next(pool.Length)];
                current = picked == _current ? pool[(Array.IndexOf(pool, picked) + 1) % pool.Length] : picked;
                _current = current;
                _next = now.AddMinutes(Random.Shared.Next(15, 26));
                _since = Stopwatch.GetTimestamp();
            }

            return (current, _since);
        }
    }

    private static string[] ForHour(int hour) => hour switch
    {
        >= 5 and < 12 => Morning,
        >= 12 and < 17 => Afternoon,
        >= 17 and < 23 => Evening,
        _ => Night
    };
}
