using System.Diagnostics;
using System.Globalization;

namespace Bridge.Extensions;

public static class DateTimeExtensions
{
    private static readonly DateTime ANCHOR_UTC = DateTime.UtcNow;
    private static readonly long ANCHOR_TICKS = Stopwatch.GetTimestamp();
    private static readonly double TICK_FREQUENCY = Stopwatch.Frequency;

    public static string IsoNow => HighResUtcNow.ToString("o", CultureInfo.InvariantCulture);

    public static DateTime HighResUtcNow
    {
        get
        {
            var elapsed = (Stopwatch.GetTimestamp() - ANCHOR_TICKS) / TICK_FREQUENCY;
            return ANCHOR_UTC.AddSeconds(elapsed);
        }
    }

    public static string ToIso(this DateTime dateTime)
    {
        return dateTime.ToString("o", CultureInfo.InvariantCulture);
    }
}