#nullable enable
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiTaskbarUsage;

public sealed class SettingsStore
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SettingsStore()
    {
        _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AI Taskbar Usage", "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), _json) ?? new AppSettings();
            var showClaude = settings.ShowClaude;
            var showCodex = settings.ShowCodex;
            if (!showClaude && !showCodex)
            {
                showClaude = true;
            }
            return settings with
            {
                RefreshIntervalSeconds = Math.Clamp(settings.RefreshIntervalSeconds, 30, 900),
                ShowClaude = showClaude,
                ShowCodex = showCodex,
                MonitorSelection = string.IsNullOrWhiteSpace(settings.MonitorSelection) ? "Primary" : settings.MonitorSelection
            };
        }
        catch (Exception ex)
        {
            SafeLogger.Write("settings", "Could not read settings: " + ex.GetType().Name);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            WriteAtomic(_filePath, JsonSerializer.Serialize(settings, _json));
        }
        catch (Exception ex)
        {
            SafeLogger.Write("settings", "Could not save settings: " + ex.GetType().Name);
        }
    }

    internal static void WriteAtomic(string path, string content)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Missing settings directory.");
        Directory.CreateDirectory(directory);
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, true);
    }
}

public sealed class CacheStore
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public CacheStore()
    {
        _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AI Taskbar Usage", "usage-cache.json");
    }

    public IReadOnlyDictionary<ServiceKind, UsageSnapshot> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new Dictionary<ServiceKind, UsageSnapshot>();
            }

            var envelope = JsonSerializer.Deserialize<CacheEnvelope>(File.ReadAllText(_filePath), _json);
            return envelope?.Snapshots
                       .GroupBy(x => x.Service)
                       .ToDictionary(group => group.Key, group => group.Last())
                   ?? new Dictionary<ServiceKind, UsageSnapshot>();
        }
        catch (Exception ex)
        {
            SafeLogger.Write("cache", "Could not read usage cache: " + ex.GetType().Name);
            return new Dictionary<ServiceKind, UsageSnapshot>();
        }
    }

    public void Save(IEnumerable<UsageSnapshot> snapshots)
    {
        try
        {
            var envelope = new CacheEnvelope { Snapshots = snapshots.ToList() };
            SettingsStore.WriteAtomic(_filePath, JsonSerializer.Serialize(envelope, _json));
        }
        catch (Exception ex)
        {
            SafeLogger.Write("cache", "Could not save usage cache: " + ex.GetType().Name);
        }
    }
}
