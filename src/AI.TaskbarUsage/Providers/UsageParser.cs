#nullable enable
using System.Globalization;
using System.Text.Json;

namespace AiTaskbarUsage;

public static class UsageParser
{
    public static bool TryParse(JsonDocument document, DateTimeOffset observedAt, out ParsedUsage usage)
    {
        QuotaWindow? fiveHour = null;
        QuotaWindow? weekly = null;
        Visit(document.RootElement, string.Empty, observedAt, ref fiveHour, ref weekly);
        usage = new ParsedUsage(fiveHour, weekly);
        return fiveHour is not null || weekly is not null;
    }

    private static void Visit(JsonElement element, string context, DateTimeOffset observedAt, ref QuotaWindow? fiveHour, ref QuotaWindow? weekly)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var percent = FindNumber(element, "used_percent", "usedPercent", "utilization", "percentage");
            if (percent is not null && percent is >= 0 and <= 100)
            {
                var seconds = FindNumber(element, "limit_window_seconds", "limitWindowSeconds");
                var window = new QuotaWindow(percent, FindReset(element, observedAt));
                if (IsWeekly(context, seconds))
                {
                    weekly ??= window;
                }
                else if (IsFiveHour(context, seconds))
                {
                    fiveHour ??= window;
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                var nextContext = string.IsNullOrEmpty(context) ? property.Name : context + "." + property.Name;
                Visit(property.Value, nextContext, observedAt, ref fiveHour, ref weekly);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                Visit(child, context, observedAt, ref fiveHour, ref weekly);
            }
        }
    }

    private static bool IsWeekly(string context, double? seconds)
    {
        return (seconds is >= 172800) || ContainsAny(context, "seven_day", "seven-day", "7d", "weekly", "week", "secondary");
    }

    private static bool IsFiveHour(string context, double? seconds)
    {
        return (seconds is > 0 and < 172800) || ContainsAny(context, "five_hour", "five-hour", "5h", "primary");
    }

    private static bool ContainsAny(string value, params string[] parts) => parts.Any(part => value.Contains(part, StringComparison.OrdinalIgnoreCase));

    private static double? FindNumber(JsonElement element, params string[] names)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Any(name => property.NameEquals(name)))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out var number))
            {
                return number;
            }

            if (property.Value.ValueKind == JsonValueKind.String && double.TryParse(property.Value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }
        }

        return null;
    }

    private static DateTimeOffset? FindReset(JsonElement element, DateTimeOffset observedAt)
    {
        foreach (var name in new[] { "reset_at", "resetAt", "resets_at", "resetsAt" })
        {
            foreach (var property in element.EnumerateObject().Where(x => x.NameEquals(name)))
            {
                if (property.Value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(property.Value.GetString(), out var date))
                {
                    return date;
                }

                if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt64(out var epoch))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(epoch);
                }
            }
        }

        var resetAfter = FindNumber(element, "reset_after_seconds", "resetAfterSeconds");
        return resetAfter is >= 0 ? observedAt.AddSeconds(resetAfter.Value) : null;
    }
}
