using System.Globalization;
using System.Text.Json;
using AiTaskbarUsage;

namespace AiTaskbarUsage.Tests;

public sealed class UsageParserTests
{
    [Fact]
    public void ParsesClaudeNamedWindowsAndResetTimes()
    {
        using var document = JsonDocument.Parse("""
        {
          "five_hour": { "utilization": 62.5, "resets_at": "2026-10-12T14:30:00Z" },
          "seven_day": { "utilization": 28, "resets_at": "2026-10-18T12:00:00Z" }
        }
        """);

        Assert.True(UsageParser.TryParse(document, DateTimeOffset.Parse("2026-10-12T12:00:00Z", CultureInfo.InvariantCulture), out var result));
        Assert.Equal(62.5, result.FiveHour!.UsedPercent);
        Assert.Equal(28, result.Weekly!.UsedPercent);
        Assert.Equal(DateTimeOffset.Parse("2026-10-12T14:30:00Z", CultureInfo.InvariantCulture), result.FiveHour.ResetAt);
    }

    [Fact]
    public void ClassifiesCodexWindowsByDuration()
    {
        using var document = JsonDocument.Parse("""
        {
          "rate_limit": {
            "primary_window": { "used_percent": 35, "limit_window_seconds": 18000, "reset_after_seconds": 1200 },
            "secondary_window": { "used_percent": 14, "limit_window_seconds": 604800, "reset_at": 1792324800 }
          }
        }
        """);

        var observed = DateTimeOffset.Parse("2026-10-12T12:00:00Z", CultureInfo.InvariantCulture);
        Assert.True(UsageParser.TryParse(document, observed, out var result));
        Assert.Equal(35, result.FiveHour!.UsedPercent);
        Assert.Equal(14, result.Weekly!.UsedPercent);
        Assert.Equal(observed.AddSeconds(1200), result.FiveHour.ResetAt);
    }

    [Fact]
    public void RejectsOutOfBoundsPercentagesInsteadOfClamping()
    {
        using var document = JsonDocument.Parse("""{ "five_hour": { "utilization": 101 } }""");

        Assert.False(UsageParser.TryParse(document, DateTimeOffset.UtcNow, out _));
    }
}
