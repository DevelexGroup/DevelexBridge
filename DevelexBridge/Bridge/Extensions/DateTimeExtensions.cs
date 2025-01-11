using System.Globalization;

namespace Bridge.Extensions;

public static class DateTimeExtensions
{
    public static string IsoNow => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

    public static string ToIso(this DateTime dateTime)
    {
        return dateTime.ToString("o", CultureInfo.InvariantCulture);
    }
}