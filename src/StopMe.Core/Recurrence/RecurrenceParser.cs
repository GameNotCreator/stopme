using System.Globalization;
using StopMe.Core.Enums;

namespace StopMe.Core.Recurrence;

public static class RecurrenceParser
{
    private static readonly Dictionary<string, DayOfWeek> DayCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MO"] = DayOfWeek.Monday,
        ["TU"] = DayOfWeek.Tuesday,
        ["WE"] = DayOfWeek.Wednesday,
        ["TH"] = DayOfWeek.Thursday,
        ["FR"] = DayOfWeek.Friday,
        ["SA"] = DayOfWeek.Saturday,
        ["SU"] = DayOfWeek.Sunday,
    };

    public static RecurrencePattern Parse(string rrule)
    {
        ArgumentNullException.ThrowIfNull(rrule);

        var trimmed = rrule.Trim();
        if (trimmed.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed["RRULE:".Length..];

        var parts = trimmed.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in parts)
        {
            var eq = part.IndexOf('=', StringComparison.Ordinal);
            if (eq < 0) continue;
            values[part[..eq].Trim()] = part[(eq + 1)..].Trim();
        }

        if (!values.TryGetValue("FREQ", out var freqRaw))
            throw new FormatException("RRULE is missing FREQ.");

        var frequency = freqRaw.ToUpperInvariant() switch
        {
            "DAILY" => RecurrenceFrequency.Daily,
            "WEEKLY" => RecurrenceFrequency.Weekly,
            _ => throw new FormatException($"Unsupported FREQ '{freqRaw}'."),
        };

        int interval = 1;
        if (values.TryGetValue("INTERVAL", out var intervalRaw))
        {
            if (!int.TryParse(intervalRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out interval) || interval < 1)
                throw new FormatException($"Invalid INTERVAL '{intervalRaw}'.");
        }

        HashSet<DayOfWeek>? byDay = null;
        if (values.TryGetValue("BYDAY", out var byDayRaw))
        {
            byDay = new HashSet<DayOfWeek>();
            foreach (var code in byDayRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!DayCodes.TryGetValue(code, out var day))
                    throw new FormatException($"Unknown BYDAY code '{code}'.");
                byDay.Add(day);
            }
            if (byDay.Count == 0) byDay = null;
        }

        DateTime? until = null;
        if (values.TryGetValue("UNTIL", out var untilRaw))
        {
            if (!TryParseIcalDateTime(untilRaw, out var parsedUntil))
                throw new FormatException($"Invalid UNTIL value '{untilRaw}'.");
            until = parsedUntil;
        }

        int? count = null;
        if (values.TryGetValue("COUNT", out var countRaw))
        {
            if (!int.TryParse(countRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c) || c < 1)
                throw new FormatException($"Invalid COUNT '{countRaw}'.");
            count = c;
        }

        return new RecurrencePattern
        {
            Frequency = frequency,
            Interval = interval,
            ByDay = byDay,
            UntilUtc = until,
            Count = count,
        };
    }

    public static bool TryParse(string? rrule, out RecurrencePattern? pattern)
    {
        pattern = null;
        if (string.IsNullOrWhiteSpace(rrule)) return false;
        try
        {
            pattern = Parse(rrule);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryParseIcalDateTime(string value, out DateTime result)
    {
        var formats = new[] { "yyyyMMddTHHmmssZ", "yyyyMMddTHHmmss", "yyyyMMdd" };
        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
            return true;
        result = default;
        return false;
    }

    public static string Format(RecurrencePattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var parts = new List<string>
        {
            "FREQ=" + (pattern.Frequency == RecurrenceFrequency.Daily ? "DAILY" : "WEEKLY"),
        };

        if (pattern.Interval > 1)
            parts.Add("INTERVAL=" + pattern.Interval.ToString(CultureInfo.InvariantCulture));

        if (pattern.ByDay is { Count: > 0 } days)
        {
            var ordered = days
                .OrderBy(d => ((int)d + 6) % 7)
                .Select(d => DayCodes.First(kv => kv.Value == d).Key);
            parts.Add("BYDAY=" + string.Join(',', ordered));
        }

        if (pattern.UntilUtc.HasValue)
            parts.Add("UNTIL=" + pattern.UntilUtc.Value.ToUniversalTime().ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture));

        if (pattern.Count.HasValue)
            parts.Add("COUNT=" + pattern.Count.Value.ToString(CultureInfo.InvariantCulture));

        return string.Join(';', parts);
    }
}
