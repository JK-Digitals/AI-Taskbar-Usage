using AiTaskbarUsage;

namespace AiTaskbarUsage.Tests;

public sealed class SnapshotTests
{
    [Fact]
    public void StaleSnapshotKeepsValuesButIsMarkedStale()
    {
        var snapshot = new UsageSnapshot(
            ServiceKind.Codex,
            ProviderState.Offline,
            new QuotaWindow(35, DateTimeOffset.UtcNow.AddHours(2)),
            new QuotaWindow(14, DateTimeOffset.UtcNow.AddDays(2)),
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow,
            true,
            "Codex is unreachable.");

        Assert.True(snapshot.IsStale);
        Assert.Equal(35, snapshot.FiveHour.UsedPercent);
        Assert.Equal(ProviderState.Offline, snapshot.State);
    }
}
