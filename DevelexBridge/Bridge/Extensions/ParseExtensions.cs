using System.Globalization;

namespace Bridge.Extensions;

public static class ParseExtensions
{
    public static double ParseDouble(this string input)
    {
        return double.TryParse(input, out var result) ? result : 0;
    }

    public static int ParseInt(this string input)
    {
        return int.TryParse(input, out var result) ? result : 0;
    }
}