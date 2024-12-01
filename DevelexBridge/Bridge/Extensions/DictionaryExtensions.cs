namespace Bridge.Extensions;

public static class DictionaryExtensions
{
    public static TV Get<TK, TV>(this Dictionary<TK, TV> dictionary, TK key, TV defaultValue = default(TV)) where TK : notnull
    {
        if (dictionary.TryGetValue(key, out var value))
        {
            return value ?? defaultValue;
        }

        return defaultValue;
    }
}