using System.Globalization;
using System.Text.RegularExpressions;
using Domain.Entities;

namespace Application.Recommendations;

internal static class ComponentSpecParser
{
    internal static Dictionary<string, string> ToSpecDictionary(IEnumerable<ProductVariantSpecification> specs)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in specs)
        {
            var name = s.SpecificationType?.Name?.Trim();
            var value = s.Value?.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                continue;

            if (!dict.ContainsKey(name))
                dict[name] = value;
        }
        return dict;
    }

    internal static string GetString(Dictionary<string, string> specs, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (specs.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        // fuzzy contains match
        foreach (var k in keys)
        {
            var match = specs.FirstOrDefault(p => p.Key.Contains(k, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Value))
                return match.Value.Trim();
        }

        return "";
    }

    internal static int GetInt(Dictionary<string, string> specs, params string[] keys)
        => TryGetNumber(specs, keys, out var n) ? (int)Math.Round(n) : 0;

    internal static double GetDouble(Dictionary<string, string> specs, params string[] keys)
        => TryGetNumber(specs, keys, out var n) ? n : 0d;

    private static bool TryGetNumber(Dictionary<string, string> specs, string[] keys, out double number)
    {
        var raw = GetString(specs, keys);
        if (string.IsNullOrWhiteSpace(raw))
        {
            number = 0;
            return false;
        }

        // Keep first number-like token (supports "16 GB", "4.2GHz", "3200 MHz", "250W", "305 mm")
        var m = Regex.Match(raw, @"-?\d+(?:[.,]\d+)?");
        if (!m.Success)
        {
            number = 0;
            return false;
        }

        var normalized = m.Value.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out number);
    }
}

