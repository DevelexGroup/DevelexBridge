using System.Diagnostics;
using System.Globalization;

namespace Bridge.Extensions;

public static class DateTimeExtensions
{
    private static DateTime _anchorUtc = DateTime.UtcNow;
    private static long _anchorTicks = Stopwatch.GetTimestamp();
    private static readonly double TICK_FREQUENCY = Stopwatch.Frequency;

    public static void ResetAnchor()
    {
        _anchorUtc = DateTime.UtcNow;
        _anchorTicks = Stopwatch.GetTimestamp();
    }
    
    public static string IsoNow => HighResUtcNow.ToString("o", CultureInfo.InvariantCulture);

    public static DateTime HighResUtcNow
    {
        get
        {
            var elapsed = (Stopwatch.GetTimestamp() - _anchorTicks) / TICK_FREQUENCY;
            return _anchorUtc.AddSeconds(elapsed);
        }
    }

    public static string ToIso(this DateTime dateTime)
    {
        return dateTime.ToString("o", CultureInfo.InvariantCulture);
    }
}