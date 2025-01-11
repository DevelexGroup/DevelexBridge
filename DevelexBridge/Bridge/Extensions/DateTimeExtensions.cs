using System.Globalization;

namespace Bridge.Extensions;

public static class DateTimeExtensions
{
    public static string IsoNow => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
}