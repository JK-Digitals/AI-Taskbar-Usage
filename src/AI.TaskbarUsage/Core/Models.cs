#nullable enable
using System.Text.Json.Serialization;

namespace AiTaskbarUsage;

public enum ServiceKind
{
    Claude,
    Codex
}

public enum ProviderState
{
    Unknown,
    Success,
    NoLogin,
    ApiKeyOnly,
    Unauthorized,
    RateLimited,
    Offline,
    Error
}

public enum LayoutMode
{
    Detailed,
    Compact
}

public enum ThemeMode
{
    System,
    Dark,
    Light
}

public enum TaskbarPosition
{
    BeforeNotificationArea,
    Left,
    Center,
    Right
}

public sealed record QuotaWindow(double? UsedPercent, DateTimeOffset? ResetAt)
{
    public static QuotaWindow Missing => new(null, null);
}

public sealed record UsageSnapshot(
    ServiceKind Service,
    ProviderState State,
    QuotaWindow FiveHour,
    QuotaWindow Weekly,
    DateTimeOffset? LastSuccessfulRefresh,
    DateTimeOffset ObservedAt,
    bool IsStale,
    string? Message)
{
    public static UsageSnapshot Empty(ServiceKind service, ProviderState state, string? message = null) =>
        new(service, state, QuotaWindow.Missing, QuotaWindow.Missing, null, DateTimeOffset.UtcNow, false, message);
}

public sealed record ParsedUsage(QuotaWindow? FiveHour, QuotaWindow? Weekly);

public sealed record ProviderResult(
    ServiceKind Service,
    ProviderState State,
    ParsedUsage? Usage,
    string? Message,
    DateTimeOffset ObservedAt)
{
    public static ProviderResult Failure(ServiceKind service, ProviderState state, string message) =>
        new(service, state, null, message, DateTimeOffset.UtcNow);
}

public sealed record AppSettings
{
    public int RefreshIntervalSeconds { get; init; } = 180;
    public LayoutMode Layout { get; init; } = LayoutMode.Detailed;
    public ThemeMode Theme { get; init; } = ThemeMode.System;
    public bool ShowClaude { get; init; } = true;
    public bool ShowCodex { get; init; } = true;
    public string MonitorSelection { get; init; } = "Primary";
    public TaskbarPosition Position { get; init; } = TaskbarPosition.BeforeNotificationArea;
    public int PositionOffsetDip { get; init; }
    public bool HideInFullscreen { get; init; }
    public bool AutoStart { get; init; }

    [JsonIgnore]
    public TimeSpan RefreshInterval => TimeSpan.FromSeconds(Math.Clamp(RefreshIntervalSeconds, 30, 900));
}

public sealed class CacheEnvelope
{
    public List<UsageSnapshot> Snapshots { get; set; } = [];
}
