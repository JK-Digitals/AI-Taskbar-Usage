#nullable enable
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AiTaskbarUsage;

public sealed class MainController : IDisposable
{
    private readonly System.Windows.Application _application;
    private readonly SettingsStore _settingsStore = new();
    private readonly CacheStore _cacheStore = new();
    private readonly TaskbarPlacementService _placementService = new();
    private readonly Dictionary<ServiceKind, UsageSnapshot> _snapshots = new();
    private readonly Dictionary<ServiceKind, DateTimeOffset> _backoffUntil = new();
    private readonly Dictionary<ServiceKind, int> _failureCounts = new();
    private readonly Dictionary<ServiceKind, DateTimeOffset> _lastProviderAttempt = new();
    private readonly Dictionary<IntPtr, TaskbarWidgetWindow> _widgets = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<IUsageProvider> _providers;
    private readonly DispatcherTimer _layoutTimer;
    private DispatcherTimer? _refreshTimer;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private AppSettings _settings;
    private DetailsWindow? _detailsWindow;
    private SettingsWindow? _settingsWindow;
    private bool _disposed;
    private int _missingPlacementTicks;

    public MainController(System.Windows.Application application)
    {
        _application = application;
        _settings = _settingsStore.Load();
        foreach (var pair in _cacheStore.Load())
        {
            _snapshots[pair.Key] = pair.Value with
            {
                IsStale = true,
                Message = string.IsNullOrWhiteSpace(pair.Value.Message) ? "Cached data; refreshing…" : pair.Value.Message
            };
        }

        _snapshots.TryAdd(ServiceKind.Claude, UsageSnapshot.Empty(ServiceKind.Claude, ProviderState.NoLogin));
        _snapshots.TryAdd(ServiceKind.Codex, UsageSnapshot.Empty(ServiceKind.Codex, ProviderState.NoLogin));

        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AI-Taskbar-Usage/1.0");
        _providers = [new AnthropicUsageProvider(_httpClient), new CodexUsageProvider(_httpClient)];

        _layoutTimer = new DispatcherTimer(DispatcherPriority.Background, _application.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _layoutTimer.Tick += (_, _) => RefreshLayout();
    }

    public void Start()
    {
        ConfigureTrayIcon();
        ConfigureRefreshTimer();
        _layoutTimer.Start();
        RefreshLayout();
        _ = RefreshAllAsync(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        _layoutTimer.Stop();
        _refreshTimer?.Stop();
        _trayIcon?.Dispose();
        foreach (var widget in _widgets.Values.ToList())
        {
            widget.Close();
        }
        _widgets.Clear();
        _detailsWindow?.Close();
        _settingsWindow?.Close();
        _httpClient.Dispose();
        _refreshGate.Dispose();
        _shutdown.Dispose();
    }

    private void ConfigureTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "AI Taskbar Usage"
        };
        _trayIcon.ContextMenuStrip = BuildTrayMenu();
        _trayIcon.DoubleClick += (_, _) => OpenDetails();
    }

    private System.Windows.Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Refresh", null, (_, _) => _ = RefreshAllAsync(true));
        menu.Items.Add("Show details", null, (_, _) => OpenDetails());
        menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var models = new System.Windows.Forms.ToolStripMenuItem("Displayed AIs");
        AddTrayServiceItem(models, ServiceKind.Claude, "Claude Code", _settings.ShowClaude);
        AddTrayServiceItem(models, ServiceKind.Codex, "OpenAI Codex", _settings.ShowCodex);
        menu.Items.Add(models);

        var compact = new System.Windows.Forms.ToolStripMenuItem("Compact layout") { Checked = _settings.Layout == LayoutMode.Compact };
        compact.Click += (_, _) => SetLayout(LayoutMode.Compact);
        menu.Items.Add(compact);
        var detailed = new System.Windows.Forms.ToolStripMenuItem("Detailed layout") { Checked = _settings.Layout == LayoutMode.Detailed };
        detailed.Click += (_, _) => SetLayout(LayoutMode.Detailed);
        menu.Items.Add(detailed);

        var startup = new System.Windows.Forms.ToolStripMenuItem("Start with Windows") { Checked = _settings.AutoStart || StartupManager.IsEnabled() };
        startup.Click += (_, _) => SetAutoStart(!startup.Checked);
        menu.Items.Add(startup);
        menu.Items.Add("Open logs", null, (_, _) => OpenLogs());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => _application.Shutdown());
        return menu;
    }

    private ContextMenu BuildWidgetMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(MenuItem("Refresh", (_, _) => _ = RefreshAllAsync(true)));
        menu.Items.Add(MenuItem("Settings...", (_, _) => OpenSettings()));

        var models = new MenuItem { Header = "Displayed AIs" };
        AddWidgetServiceItem(models, ServiceKind.Claude, "Claude Code", _settings.ShowClaude);
        AddWidgetServiceItem(models, ServiceKind.Codex, "OpenAI Codex", _settings.ShowCodex);
        menu.Items.Add(models);

        var open = new MenuItem { Header = "Open provider" };
        open.Items.Add(MenuItem("Claude", (_, _) => OpenProviderPage(ServiceKind.Claude)));
        open.Items.Add(MenuItem("Codex", (_, _) => OpenProviderPage(ServiceKind.Codex)));
        menu.Items.Add(open);

        menu.Items.Add(new Separator());
        var layout = new MenuItem { Header = "Layout" };
        var compact = MenuItem("Compact", (_, _) => SetLayout(LayoutMode.Compact));
        var detailed = MenuItem("Detailed", (_, _) => SetLayout(LayoutMode.Detailed));
        compact.IsCheckable = true;
        detailed.IsCheckable = true;
        compact.IsChecked = _settings.Layout == LayoutMode.Compact;
        detailed.IsChecked = _settings.Layout == LayoutMode.Detailed;
        layout.Items.Add(compact);
        layout.Items.Add(detailed);
        menu.Items.Add(layout);

        var theme = new MenuItem { Header = "Theme" };
        AddThemeItem(theme, ThemeMode.System, "System");
        AddThemeItem(theme, ThemeMode.Dark, "Dark");
        AddThemeItem(theme, ThemeMode.Light, "Light");
        menu.Items.Add(theme);

        var position = new MenuItem { Header = "Taskbar position" };
        AddPositionItem(position, TaskbarPosition.BeforeNotificationArea, "Before notification area");
        AddPositionItem(position, TaskbarPosition.Left, "Left");
        AddPositionItem(position, TaskbarPosition.Center, "Center");
        AddPositionItem(position, TaskbarPosition.Right, "Right");
        menu.Items.Add(position);

        var interval = new MenuItem { Header = "Refresh interval" };
        foreach (var seconds in new[] { 30, 60, 120, 300 })
        {
            var item = new MenuItem { Header = $"{seconds} seconds", IsCheckable = true, IsChecked = _settings.RefreshIntervalSeconds == seconds };
            var selected = seconds;
            item.Click += (_, _) => SetRefreshInterval(selected);
            interval.Items.Add(item);
        }
        menu.Items.Add(interval);

        var startup = new MenuItem { Header = "Start with Windows", IsCheckable = true, IsChecked = _settings.AutoStart || StartupManager.IsEnabled() };
        startup.Click += (_, _) => SetAutoStart(!startup.IsChecked);
        menu.Items.Add(startup);
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("Open sanitized logs", (_, _) => OpenLogs()));
        menu.Items.Add(MenuItem("Exit", (_, _) => _application.Shutdown()));
        return menu;
    }

    private static MenuItem MenuItem(string header, RoutedEventHandler handler)
    {
        var item = new MenuItem { Header = header };
        item.Click += handler;
        return item;
    }

    private void RefreshLayout()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var effectiveLayout = _settings.Layout;
            var placements = _placementService.FindPlacements(
                effectiveLayout,
                _settings.Position,
                _settings.PositionOffsetDip,
                _settings.HideInFullscreen,
                _settings.MonitorSelection,
                _settings.ShowClaude,
                _settings.ShowCodex);
            if (placements.Count == 0 && effectiveLayout == LayoutMode.Detailed)
            {
                placements = _placementService.FindPlacements(
                    LayoutMode.Compact,
                    _settings.Position,
                    _settings.PositionOffsetDip,
                    _settings.HideInFullscreen,
                    _settings.MonitorSelection,
                    _settings.ShowClaude,
                    _settings.ShowCodex);
                if (placements.Count > 0)
                {
                    effectiveLayout = LayoutMode.Compact;
                }
            }

            if (placements.Count == 0 && !_settings.HideInFullscreen)
            {
                // Keep the last widget alive during Explorer/taskbar transitions instead of closing it.
                // The next layout tick will move it to the fresh taskbar handle.
                return;
            }

            if (placements.Count == 0)
            {
                _missingPlacementTicks++;
                if (_widgets.Count > 0 && _missingPlacementTicks < 8)
                {
                    return;
                }
            }
            else
            {
                _missingPlacementTicks = 0;
            }

            var activeHandles = placements.Select(x => x.TaskbarHandle).ToHashSet();
            foreach (var stale in _widgets.Where(x => !activeHandles.Contains(x.Key)).ToList())
            {
                stale.Value.Hide();
                stale.Value.Close();
                _widgets.Remove(stale.Key);
            }

            foreach (var placement in placements)
            {
                if (!_widgets.TryGetValue(placement.TaskbarHandle, out var widget) ||
                    !widget.HasValidNativeWindow)
                {
                    if (widget is not null)
                    {
                        try
                        {
                            widget.Close();
                        }
                        catch
                        {
                            // Explorer may already have destroyed the native child window.
                        }
                    }

                    widget = new TaskbarWidgetWindow();
                    widget.DetailsRequested += (_, _) => OpenDetails();
                    widget.SetContextMenu(BuildWidgetMenu());
                    _widgets[placement.TaskbarHandle] = widget;
                    widget.ApplyVisibleServices(_settings.ShowClaude, _settings.ShowCodex);
                    widget.ApplyLayout(effectiveLayout);
                    widget.SetSnapshots(_snapshots[ServiceKind.Claude], _snapshots[ServiceKind.Codex]);
                }

                widget.ApplyVisibleServices(_settings.ShowClaude, _settings.ShowCodex);
                widget.ApplyLayout(effectiveLayout);
                widget.ApplyTheme(_settings.Theme);
                widget.SetSnapshots(_snapshots[ServiceKind.Claude], _snapshots[ServiceKind.Codex]);
                widget.SetNativePosition(placement);
            }
        }
        catch (Exception ex)
        {
            SafeLogger.Write("taskbar", "Placement update failed: " + ex.GetType().Name);
        }
    }

    private void ConfigureRefreshTimer()
    {
        _refreshTimer?.Stop();
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background, _application.Dispatcher)
        {
            Interval = _settings.RefreshInterval
        };
        _refreshTimer.Tick += (_, _) => _ = RefreshAllAsync(false);
        _refreshTimer.Start();
    }

    private async Task RefreshAllAsync(bool manual)
    {
        if (_disposed || !await _refreshGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var jobs = _providers
                .Where(provider => IsServiceEnabled(provider.Service))
                .Select(provider => RefreshProviderAsync(provider, manual, timeout.Token));
            await Task.WhenAll(jobs).ConfigureAwait(true);
            _cacheStore.Save(_snapshots.Values);
            RefreshLayout();
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task RefreshProviderAsync(IUsageProvider provider, bool manual, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (!manual && provider.Service == ServiceKind.Claude &&
            _lastProviderAttempt.TryGetValue(provider.Service, out var lastAttempt) &&
            lastAttempt.AddSeconds(180) > now)
        {
            return;
        }

        if (!manual && _backoffUntil.TryGetValue(provider.Service, out var until) && until > now)
        {
            return;
        }

        _lastProviderAttempt[provider.Service] = now;
        ProviderResult result;
        try
        {
            result = await provider.RefreshAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            SafeLogger.Write(provider.Service.ToString(), "Refresh failed: " + ex.GetType().Name);
            result = ProviderResult.Failure(provider.Service, ProviderState.Error, "Refresh failed safely; see sanitized logs.");
        }

        if (result.State == ProviderState.Success)
        {
            _failureCounts[provider.Service] = 0;
            _backoffUntil.Remove(provider.Service);
        }
        else if (result.State is ProviderState.Offline or ProviderState.Error or ProviderState.RateLimited)
        {
            var failures = _failureCounts.TryGetValue(provider.Service, out var count) ? count + 1 : 1;
            _failureCounts[provider.Service] = failures;
            _backoffUntil[provider.Service] = now.AddSeconds(Math.Min(600, 30 * Math.Pow(2, failures - 1)));
        }

        ApplyResult(result);
    }

    private void ApplyResult(ProviderResult result)
    {
        var previous = _snapshots[result.Service];
        var hasData = result.Usage is not null;
        var snapshot = hasData
            ? new UsageSnapshot(result.Service, ProviderState.Success,
                result.Usage!.FiveHour ?? previous.FiveHour,
                result.Usage.Weekly ?? previous.Weekly,
                result.ObservedAt, result.ObservedAt, false, null)
            : new UsageSnapshot(result.Service, result.State, previous.FiveHour, previous.Weekly,
                previous.LastSuccessfulRefresh, result.ObservedAt,
                previous.FiveHour.UsedPercent is not null || previous.Weekly.UsedPercent is not null,
                result.Message);
        _snapshots[result.Service] = snapshot;
        UpdateWidgets();
    }

    private void UpdateWidgets()
    {
        foreach (var widget in _widgets.Values)
        {
            widget.SetSnapshots(_snapshots[ServiceKind.Claude], _snapshots[ServiceKind.Codex]);
        }
    }

    private void OpenDetails()
    {
        if (_detailsWindow is { IsVisible: true })
        {
            _detailsWindow.Activate();
            return;
        }

        _detailsWindow = new DetailsWindow(
            _snapshots[ServiceKind.Claude],
            _snapshots[ServiceKind.Codex],
            _settings.ShowClaude,
            _settings.ShowCodex,
            () => _ = RefreshAllAsync(true), () => OpenProviderPage(ServiceKind.Claude), () => OpenProviderPage(ServiceKind.Codex));
        _detailsWindow.Closed += (_, _) => _detailsWindow = null;
        _detailsWindow.Show();
    }

    private void SetLayout(LayoutMode layout)
    {
        ApplySettings(_settings with { Layout = layout });
    }

    private void SetRefreshInterval(int seconds)
    {
        ApplySettings(_settings with { RefreshIntervalSeconds = Math.Clamp(seconds, 30, 900) });
    }

    private void SetAutoStart(bool enabled)
    {
        StartupManager.SetEnabled(enabled);
        ApplySettings(_settings with { AutoStart = enabled });
    }

    private void AddThemeItem(MenuItem parent, ThemeMode theme, string header)
    {
        var item = MenuItem(header, (_, _) => ApplySettings(_settings with { Theme = theme }));
        item.IsCheckable = true;
        item.IsChecked = _settings.Theme == theme;
        parent.Items.Add(item);
    }

    private void AddPositionItem(MenuItem parent, TaskbarPosition position, string header)
    {
        var item = MenuItem(header, (_, _) => ApplySettings(_settings with { Position = position }));
        item.IsCheckable = true;
        item.IsChecked = _settings.Position == position;
        parent.Items.Add(item);
    }

    private void OpenSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, ApplySettings);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void ApplySettings(AppSettings settings)
    {
        var visibilityChanged = settings.ShowClaude != _settings.ShowClaude || settings.ShowCodex != _settings.ShowCodex;
        var showClaude = settings.ShowClaude;
        var showCodex = settings.ShowCodex;
        if (!showClaude && !showCodex)
        {
            showClaude = true;
        }

        _settings = settings with
        {
            RefreshIntervalSeconds = Math.Clamp(settings.RefreshIntervalSeconds, 30, 900),
            PositionOffsetDip = Math.Clamp(settings.PositionOffsetDip, -1000, 1000),
            ShowClaude = showClaude,
            ShowCodex = showCodex
        };
        _settingsStore.Save(_settings);
        ConfigureRefreshTimer();
        _application.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RefreshMenus));
        if (visibilityChanged && _detailsWindow is not null)
        {
            _detailsWindow.Close();
            _detailsWindow = null;
        }
        RefreshLayout();
        if (visibilityChanged)
        {
            _ = RefreshAllAsync(true);
        }
    }

    private bool IsServiceEnabled(ServiceKind service) =>
        service == ServiceKind.Claude ? _settings.ShowClaude : _settings.ShowCodex;

    private void SetServiceVisibility(ServiceKind service, bool visible)
    {
        var showClaude = service == ServiceKind.Claude ? visible : _settings.ShowClaude;
        var showCodex = service == ServiceKind.Codex ? visible : _settings.ShowCodex;
        if (!showClaude && !showCodex)
        {
            return;
        }

        ApplySettings(_settings with { ShowClaude = showClaude, ShowCodex = showCodex });
    }

    private void AddWidgetServiceItem(MenuItem parent, ServiceKind service, string header, bool isVisible)
    {
        var item = new MenuItem
        {
            Header = header,
            IsCheckable = true,
            IsChecked = isVisible,
            IsEnabled = !isVisible || (_settings.ShowClaude && _settings.ShowCodex)
        };
        item.Click += (_, _) => SetServiceVisibility(service, !isVisible);
        parent.Items.Add(item);
    }

    private void AddTrayServiceItem(System.Windows.Forms.ToolStripMenuItem parent, ServiceKind service, string label, bool isVisible)
    {
        var item = new System.Windows.Forms.ToolStripMenuItem(label)
        {
            CheckOnClick = false,
            Checked = isVisible,
            Enabled = !isVisible || (_settings.ShowClaude && _settings.ShowCodex)
        };
        item.Click += (_, _) => SetServiceVisibility(service, !isVisible);
        parent.DropDownItems.Add(item);
    }

    private void RefreshMenus()
    {
        if (_trayIcon is not null)
        {
            var previous = _trayIcon.ContextMenuStrip;
            _trayIcon.ContextMenuStrip = BuildTrayMenu();
            previous?.Dispose();
        }

        foreach (var widget in _widgets.Values)
        {
            widget.SetContextMenu(BuildWidgetMenu());
        }
    }

    private static void OpenProviderPage(ServiceKind service)
    {
        var url = service == ServiceKind.Claude ? "https://claude.ai" : "https://chatgpt.com/codex";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SafeLogger.Write("ui", "Could not open provider page: " + ex.GetType().Name);
        }
    }

    private static void OpenLogs()
    {
        try
        {
            Directory.CreateDirectory(SafeLogger.LogDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", SafeLogger.LogDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SafeLogger.Write("ui", "Could not open log directory: " + ex.GetType().Name);
        }
    }
}
