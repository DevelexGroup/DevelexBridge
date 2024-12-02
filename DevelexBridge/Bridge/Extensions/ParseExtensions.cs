using System.Globalization;

namespace Bridge.Extensions;

public static class ParseExtensions
{
    public static double ParseDouble(this string input)
    {
        return double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    public static int ParseInt(this string input)
    {
        return int.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }
}