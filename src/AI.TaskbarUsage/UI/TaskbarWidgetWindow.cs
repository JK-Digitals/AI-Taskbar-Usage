#nullable enable
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Globalization;
using MediaColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace AiTaskbarUsage;

public sealed class TaskbarWidgetWindow
{
    private readonly Border _surface;
    private readonly Grid _serviceGrid;
    private readonly StackPanel _claudePanel;
    private readonly StackPanel _codexPanel;
    private readonly WpfRectangle _divider;
    private readonly TextBlock _claudeText;
    private readonly TextBlock _codexText;
    private readonly TextBlock _claudeCaption;
    private readonly TextBlock _codexCaption;
    private readonly Ellipse _claudeStatusDot;
    private readonly Ellipse _codexStatusDot;
    private UsageSnapshot _claude = UsageSnapshot.Empty(ServiceKind.Claude, ProviderState.NoLogin);
    private UsageSnapshot _codex = UsageSnapshot.Empty(ServiceKind.Codex, ProviderState.NoLogin);
    private LayoutMode _layout = LayoutMode.Detailed;
    private bool _showClaude = true;
    private bool _showCodex = true;
    private bool? _appliedLightTheme;
    private NativeWidgetWindow? _source;
    private IntPtr _taskbarHandle;
    private ContextMenu? _contextMenu;
    private int _pixelWidth;
    private int _pixelHeight;

    public event EventHandler? DetailsRequested;

    public bool HasValidNativeWindow
    {
        get
        {
            var handle = _source?.Handle ?? IntPtr.Zero;
            return handle != IntPtr.Zero && Win32.IsWindow(handle);
        }
    }

    public TaskbarWidgetWindow()
    {
        _serviceGrid = new Grid();
        _serviceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _serviceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _serviceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _claudePanel = CreateServicePanel("Claude", out _claudeCaption, out _claudeText, out _claudeStatusDot, MediaColor.FromRgb(232, 151, 82));
        _codexPanel = CreateServicePanel("Codex", out _codexCaption, out _codexText, out _codexStatusDot, MediaColor.FromRgb(110, 176, 255));
        Grid.SetColumn(_claudePanel, 0);
        Grid.SetColumn(_codexPanel, 2);
        _serviceGrid.Children.Add(_claudePanel);
        _serviceGrid.Children.Add(_codexPanel);

        _divider = new WpfRectangle
        {
            Width = 1,
            Height = 18,
            Fill = new SolidColorBrush(MediaColor.FromArgb(80, 255, 255, 255)),
            Margin = new Thickness(5, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_divider, 1);
        _serviceGrid.Children.Add(_divider);

        _surface = new Border
        {
            Background = new SolidColorBrush(MediaColor.FromArgb(224, 30, 34, 42)),
            BorderBrush = new SolidColorBrush(MediaColor.FromArgb(120, 150, 160, 178)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(9, 3, 9, 3),
            Child = _serviceGrid,
            ToolTip = "AI Taskbar Usage"
        };

        _surface.MouseLeftButtonUp += (_, args) =>
        {
            if (args.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                DetailsRequested?.Invoke(this, EventArgs.Empty);
                args.Handled = true;
            }
        };

        ApplyTheme(ThemeMode.System);
    }

    public void ApplyLayout(LayoutMode layout)
    {
        if (_layout == layout)
        {
            return;
        }

        _layout = layout;
        _claudeCaption.Visibility = layout == LayoutMode.Compact ? Visibility.Collapsed : Visibility.Visible;
        _codexCaption.Visibility = layout == LayoutMode.Compact ? Visibility.Collapsed : Visibility.Visible;
        _surface.Padding = layout == LayoutMode.Compact ? new Thickness(7, 3, 7, 3) : new Thickness(9, 3, 9, 3);
        UpdateText();
        RenderLayered();
    }

    public void ApplyVisibleServices(bool showClaude, bool showCodex)
    {
        if (!showClaude && !showCodex)
        {
            showClaude = true;
        }

        if (_showClaude == showClaude && _showCodex == showCodex)
        {
            return;
        }

        _showClaude = showClaude;
        _showCodex = showCodex;
        _claudePanel.Visibility = showClaude ? Visibility.Visible : Visibility.Collapsed;
        _codexPanel.Visibility = showCodex ? Visibility.Visible : Visibility.Collapsed;
        _divider.Visibility = showClaude && showCodex ? Visibility.Visible : Visibility.Collapsed;
        _serviceGrid.ColumnDefinitions[0].Width = showClaude ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        _serviceGrid.ColumnDefinitions[1].Width = showClaude && showCodex ? GridLength.Auto : new GridLength(0);
        _serviceGrid.ColumnDefinitions[2].Width = showCodex ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        UpdateText();
        RenderLayered();
    }

    public void ApplyTheme() => ApplyTheme(ThemeMode.System);

    public void ApplyTheme(ThemeMode theme)
    {
        var useLightTheme = theme == ThemeMode.Light || theme == ThemeMode.System && IsSystemLightTheme();
        if (_appliedLightTheme == useLightTheme)
        {
            return;
        }

        _appliedLightTheme = useLightTheme;
        var foreground = useLightTheme ? new SolidColorBrush(MediaColor.FromRgb(28, 32, 40)) : WpfBrushes.White;
        try
        {
            var surfaceBackground = useLightTheme
                ? new SolidColorBrush(MediaColor.FromArgb(244, 247, 248, 250))
                : new SolidColorBrush(MediaColor.FromArgb(238, 27, 31, 40));
            _surface.Background = surfaceBackground;
            _surface.BorderBrush = useLightTheme
                ? new SolidColorBrush(MediaColor.FromArgb(210, 210, 216, 226))
                : new SolidColorBrush(MediaColor.FromArgb(185, 102, 112, 132));
            _claudeText.Foreground = foreground;
            _codexText.Foreground = foreground;
            _surface.Effect = null;
            RenderLayered();
        }
        catch
        {
            // Keep the last usable theme if the Windows theme registry is unavailable.
        }
    }

    public void SetSnapshots(UsageSnapshot claude, UsageSnapshot codex)
    {
        if (ReferenceEquals(_claude, claude) && ReferenceEquals(_codex, codex))
        {
            return;
        }

        _claude = claude;
        _codex = codex;
        _claudeStatusDot.Fill = StatusBrush(claude.State);
        _codexStatusDot.Fill = StatusBrush(codex.State);
        UpdateText();
        RenderLayered();
    }

    public void SetNativePosition(TaskbarPlacement placement)
    {
        if (placement.TaskbarHandle == IntPtr.Zero ||
            !Win32.IsWindowVisible(placement.TaskbarHandle) ||
            !Win32.GetWindowRect(placement.TaskbarHandle, out var taskbarRect))
        {
            return;
        }

        if (!HasValidNativeWindow || _taskbarHandle != placement.TaskbarHandle)
        {
            try
            {
                CreateNativeChild(placement, taskbarRect);
            }
            catch (Exception ex)
            {
                SafeLogger.Write("taskbar", "Could not create taskbar child host: " + ex.GetType().Name);
                Close();
                return;
            }
        }

        var handle = _source?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var sizeChanged = _pixelWidth != placement.Width || _pixelHeight != placement.Height;
        Win32.SetWindowPos(
            handle,
            Win32.HwndTop,
            placement.Left - taskbarRect.Left,
            placement.Top - taskbarRect.Top,
            placement.Width,
            placement.Height,
            Win32.SwpNoActivate | Win32.SwpNoOwnerZOrder | Win32.SwpNoSendChanging | Win32.SwpShowWindow);
        _pixelWidth = placement.Width;
        _pixelHeight = placement.Height;
        if (sizeChanged)
        {
            RenderLayered();
        }
    }

    public void SetContextMenu(ContextMenu menu)
    {
        _contextMenu = menu;
        _surface.ContextMenu = menu;
    }

    public void Hide()
    {
        var handle = _source?.Handle ?? IntPtr.Zero;
        if (handle != IntPtr.Zero && Win32.IsWindow(handle))
        {
            Win32.ShowWindow(handle, Win32.SwHide);
        }
    }

    public void Close()
    {
        var source = _source;
        _source = null;
        _taskbarHandle = IntPtr.Zero;
        _pixelWidth = 0;
        _pixelHeight = 0;
        if (source is null)
        {
            return;
        }

        try
        {
            source.Dispose();
        }
        catch
        {
            // Explorer may already have destroyed the native parent and child.
        }
    }

    private void UpdateText()
    {
        _claudeText.Text = _layout == LayoutMode.Compact ? CompactText(_claude) : DetailedText(_claude);
        _codexText.Text = _layout == LayoutMode.Compact ? CompactText(_codex) : DetailedText(_codex);
        _surface.ToolTip = BuildTooltip(_claude, _codex, _showClaude, _showCodex);
    }

    private static string DetailedText(UsageSnapshot snapshot) => $"5h {Format(snapshot, snapshot.FiveHour)}  ·  7d {Format(snapshot, snapshot.Weekly)}";

    private static string CompactText(UsageSnapshot snapshot) => $"{(snapshot.Service == ServiceKind.Claude ? "C" : "X")} {Format(snapshot, snapshot.FiveHour)} / {Format(snapshot, snapshot.Weekly)}";

    private static string Format(UsageSnapshot snapshot, QuotaWindow window)
    {
        if (snapshot.State is ProviderState.NoLogin or ProviderState.ApiKeyOnly or ProviderState.Unauthorized)
        {
            return "Login";
        }

        if (snapshot.State == ProviderState.RateLimited)
        {
            return "Wait";
        }

        if (snapshot.State == ProviderState.Offline)
        {
            return "Offline";
        }

        return window.UsedPercent is null ? "—" : $"{window.UsedPercent:0.#}%{(snapshot.IsStale ? "*" : string.Empty)}";
    }

    private static SolidColorBrush StatusBrush(ProviderState state) => state switch
    {
        ProviderState.Success => new SolidColorBrush(MediaColor.FromRgb(82, 214, 146)),
        ProviderState.RateLimited => new SolidColorBrush(MediaColor.FromRgb(246, 190, 82)),
        ProviderState.Offline => new SolidColorBrush(MediaColor.FromRgb(246, 190, 82)),
        ProviderState.NoLogin or ProviderState.Unauthorized or ProviderState.Error => new SolidColorBrush(MediaColor.FromRgb(246, 106, 116)),
        _ => new SolidColorBrush(MediaColor.FromRgb(170, 178, 194))
    };

    private static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildTooltip(UsageSnapshot claude, UsageSnapshot codex, bool showClaude, bool showCodex)
    {
        var sections = new List<string>();
        if (showClaude)
        {
            sections.Add($"Claude{Environment.NewLine}{TooltipFor(claude)}");
        }
        if (showCodex)
        {
            sections.Add($"Codex{Environment.NewLine}{TooltipFor(codex)}");
        }
        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }

    private static string TooltipFor(UsageSnapshot snapshot)
    {
        var five = DescribeWindow("5-hour", snapshot.FiveHour);
        var week = DescribeWindow("7-day", snapshot.Weekly);
        var refreshed = snapshot.LastSuccessfulRefresh is null ? "never" : snapshot.LastSuccessfulRefresh.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        var state = snapshot.IsStale ? "stale cache" : snapshot.State.ToString();
        return $"{five}; {week}{Environment.NewLine}Status: {state}{Environment.NewLine}Last refresh: {refreshed}{(string.IsNullOrWhiteSpace(snapshot.Message) ? string.Empty : Environment.NewLine + snapshot.Message)}";
    }

    private static string DescribeWindow(string label, QuotaWindow window)
    {
        var percent = window.UsedPercent is null ? "—" : $"{window.UsedPercent:0.#}%";
        var reset = window.ResetAt is null ? "reset unknown" : $"resets {window.ResetAt.Value.ToLocalTime():g}";
        return $"{label}: {percent} ({reset})";
    }

    private void CreateNativeChild(TaskbarPlacement placement, Win32.Rect taskbarRect)
    {
        Close();
        var source = new NativeWidgetWindow(
            placement.Left,
            placement.Top,
            placement.Width,
            placement.Height,
            HandleNativeMouseMessage);
        var handle = source.Handle;
        var style = Win32.GetWindowLongPtr(handle, Win32.GwlStyle).ToInt64();
        style = (style & ~Win32.WsPopup) | Win32.WsChild | Win32.WsVisible | Win32.WsClipSiblings;
        Win32.SetWindowLongPtr(handle, Win32.GwlStyle, new IntPtr(style));
        Win32.SetParent(handle, placement.TaskbarHandle);
        _source = source;
        _taskbarHandle = placement.TaskbarHandle;
        _pixelWidth = placement.Width;
        _pixelHeight = placement.Height;
        Win32.SetWindowPos(
            handle,
            Win32.HwndTop,
            placement.Left - taskbarRect.Left,
            placement.Top - taskbarRect.Top,
            placement.Width,
            placement.Height,
            Win32.SwpNoActivate | Win32.SwpNoOwnerZOrder | Win32.SwpNoSendChanging | Win32.SwpShowWindow | Win32.SwpFrameChanged);
        RenderLayered();
    }

    private void HandleNativeMouseMessage(uint message)
    {
        if (message == Win32.WmLButtonUp)
        {
            DetailsRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (message == Win32.WmRButtonUp && _contextMenu is not null)
        {
            _contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            _contextMenu.IsOpen = true;
        }
    }

    private void RenderLayered()
    {
        var handle = _source?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero || _pixelWidth <= 0 || _pixelHeight <= 0 || !Win32.IsWindow(handle))
        {
            return;
        }

        try
        {
            var dpi = Win32.GetDpiForWindow(handle);
            if (dpi == 0)
            {
                dpi = 96;
            }

            var logicalWidth = _pixelWidth * 96d / dpi;
            var logicalHeight = _pixelHeight * 96d / dpi;
            _surface.Measure(new System.Windows.Size(logicalWidth, logicalHeight));
            _surface.Arrange(new Rect(0, 0, logicalWidth, logicalHeight));
            _surface.UpdateLayout();

            var bitmap = new RenderTargetBitmap(_pixelWidth, _pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(_surface);
            var stride = _pixelWidth * 4;
            var pixels = new byte[stride * _pixelHeight];
            bitmap.CopyPixels(pixels, stride, 0);
            PushLayeredBitmap(handle, pixels, stride);
        }
        catch (Exception ex)
        {
            SafeLogger.Write("taskbar", "Layered render failed: " + ex.GetType().Name);
        }
    }

    private void PushLayeredBitmap(IntPtr handle, byte[] pixels, int stride)
    {
        var screenDc = Win32.GetDC(handle);
        var memoryDc = IntPtr.Zero;
        var dib = IntPtr.Zero;
        var oldBitmap = IntPtr.Zero;
        try
        {
            memoryDc = Win32.CreateCompatibleDC(screenDc);
            var bitmapInfo = new Win32.BitmapInfo
            {
                Header = new Win32.BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<Win32.BitmapInfoHeader>(),
                    Width = _pixelWidth,
                    Height = -_pixelHeight,
                    Planes = 1,
                    BitCount = 32,
                    Compression = Win32.BiRgb,
                    SizeImage = (uint)(stride * _pixelHeight)
                }
            };
            dib = Win32.CreateDIBSection(memoryDc, ref bitmapInfo, Win32.DibRgbColors, out var bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero || bits == IntPtr.Zero)
            {
                return;
            }

            Marshal.Copy(pixels, 0, bits, pixels.Length);
            oldBitmap = Win32.SelectObject(memoryDc, dib);
            var size = new Win32.Size { Width = _pixelWidth, Height = _pixelHeight };
            var source = new Win32.Point();
            var blend = new Win32.BlendFunction
            {
                BlendOp = Win32.AcSrcOver,
                SourceConstantAlpha = 255,
                AlphaFormat = Win32.AcSrcAlpha
            };
            if (!Win32.UpdateLayeredWindow(handle, screenDc, IntPtr.Zero, ref size, memoryDc, ref source, 0, ref blend, Win32.UlwAlpha))
            {
                SafeLogger.Write("taskbar", "UpdateLayeredWindow failed: " + Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero && memoryDc != IntPtr.Zero)
            {
                Win32.SelectObject(memoryDc, oldBitmap);
            }
            if (dib != IntPtr.Zero)
            {
                Win32.DeleteObject(dib);
            }
            if (memoryDc != IntPtr.Zero)
            {
                Win32.DeleteDC(memoryDc);
            }
            if (screenDc != IntPtr.Zero)
            {
                _ = Win32.ReleaseDC(handle, screenDc);
            }
        }
    }

    private sealed class NativeWidgetWindow : System.Windows.Forms.NativeWindow, IDisposable
    {
        private readonly Action<uint> _mouseMessage;

        public NativeWidgetWindow(int x, int y, int width, int height, Action<uint> mouseMessage)
        {
            _mouseMessage = mouseMessage;
            var parameters = new System.Windows.Forms.CreateParams
            {
                Caption = "AI Taskbar Usage Widget",
                X = x,
                Y = y,
                Width = width,
                Height = height,
                Style = unchecked((int)Win32.WsPopup),
                ExStyle = unchecked((int)(Win32.WsExNoActivate | Win32.WsExToolWindow | Win32.WsExLayered))
            };
            CreateHandle(parameters);
        }

        protected override void WndProc(ref System.Windows.Forms.Message message)
        {
            var id = unchecked((uint)message.Msg);
            if (id is Win32.WmLButtonUp or Win32.WmRButtonUp)
            {
                _mouseMessage(id);
                message.Result = IntPtr.Zero;
                return;
            }
            if (id == Win32.WmEraseBackground)
            {
                message.Result = new IntPtr(1);
                return;
            }
            if (id == Win32.WmPaint)
            {
                _ = Win32.BeginPaint(Handle, out var paint);
                _ = Win32.EndPaint(Handle, ref paint);
                message.Result = IntPtr.Zero;
                return;
            }

            base.WndProc(ref message);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                DestroyHandle();
            }
        }
    }

    private static StackPanel CreateServicePanel(string name, out TextBlock caption, out TextBlock value, out Ellipse statusDot, MediaColor accent)
    {
        statusDot = new Ellipse
        {
            Width = 7,
            Height = 7,
            Fill = new SolidColorBrush(MediaColor.FromRgb(170, 178, 194)),
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        caption = new TextBlock
        {
            Text = name.ToUpperInvariant(),
            Foreground = new SolidColorBrush(accent),
            FontSize = 8,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, -1)
        };
        value = new TextBlock
        {
            Foreground = WpfBrushes.White,
            FontSize = 10,
            FontWeight = FontWeights.Medium,
            Text = "—",
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var heading = new StackPanel { Orientation = WpfOrientation.Horizontal };
        heading.Children.Add(statusDot);
        heading.Children.Add(caption);
        return new StackPanel { Orientation = WpfOrientation.Vertical, Children = { heading, value } };
    }
}
