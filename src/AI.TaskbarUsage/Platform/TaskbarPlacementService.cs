#nullable enable
using System.Diagnostics;
using WinFormsScreen = System.Windows.Forms.Screen;

namespace AiTaskbarUsage;

public sealed record TaskbarPlacement(
    IntPtr TaskbarHandle,
    string MonitorDeviceName,
    bool IsPrimaryMonitor,
    int Left,
    int Top,
    int Width,
    int Height,
    uint Dpi);

public sealed class TaskbarPlacementService
{
    private const int HorizontalPadding = 8;
    public IReadOnlyList<TaskbarPlacement> FindPlacements(LayoutMode layout) =>
        FindPlacements(layout, TaskbarPosition.BeforeNotificationArea, 0, false, "Primary", true, true);

    public IReadOnlyList<TaskbarPlacement> FindPlacements(
        LayoutMode layout,
        TaskbarPosition position,
        int offsetDip,
        bool hideInFullscreen,
        string monitorSelection,
        bool showClaude,
        bool showCodex)
    {
        var desiredWidthDip = WidgetLayoutMetrics.WidthDip(layout, showClaude, showCodex);
        var desiredHeightDip = WidgetLayoutMetrics.HeightDip;
        var shellWindows = new List<IntPtr>();
        Win32.EnumWindows((handle, _) =>
        {
            var className = GetClassName(handle);
            if ((className == "Shell_TrayWnd" || className == "Shell_SecondaryTrayWnd") && Win32.IsWindowVisible(handle))
            {
                shellWindows.Add(handle);
            }
            return true;
        }, IntPtr.Zero);

        if (shellWindows.Count == 0)
        {
            var primary = Win32.FindWindow("Shell_TrayWnd", null);
            if (primary != IntPtr.Zero)
            {
                shellWindows.Add(primary);
            }
        }

        var placements = new List<TaskbarPlacement>();
        foreach (var shell in shellWindows.Distinct())
        {
            if (!Win32.GetWindowRect(shell, out var taskbar) || taskbar.IsEmpty || taskbar.Height < 20)
            {
                continue;
            }

            var monitor = Win32.MonitorFromWindow(shell, Win32.MonitorDefaultToNearest);
            var screen = WinFormsScreen.FromHandle(shell);
            var dpi = GetDpi(shell, monitor);
            var scale = dpi / 96d;
            var width = (int)Math.Round(desiredWidthDip * scale);
            var height = Math.Min(taskbar.Height - 4, (int)Math.Round(desiredHeightDip * scale));
            var minimumWidth = (int)Math.Round(120 * scale);
            width = Math.Min(width, Math.Max(minimumWidth, taskbar.Width - HorizontalPadding * 2));
            if (width <= 0 || height < 20)
            {
                continue;
            }

            var tray = FindChildRect(shell, "TrayNotifyWnd");
            var leftEdge = taskbar.Left + HorizontalPadding;
            var fullRightEdge = taskbar.Right - HorizontalPadding;
            var safeRightEdge = Math.Min(fullRightEdge, (tray?.Left ?? taskbar.Right) - HorizontalPadding);
            var rightEdge = position == TaskbarPosition.Right ? fullRightEdge : safeRightEdge;
            var offset = (int)Math.Round(offsetDip * scale);
            var candidateLeft = position switch
            {
                TaskbarPosition.Left => leftEdge + offset,
                TaskbarPosition.Center => taskbar.Left + ((taskbar.Width - width) / 2) + offset,
                TaskbarPosition.Right => rightEdge - width + offset,
                _ => safeRightEdge - width + offset
            };

            var maxLeft = Math.Max(leftEdge, rightEdge - width);
            candidateLeft = Math.Clamp(candidateLeft, leftEdge, maxLeft);

            var top = taskbar.Top + Math.Max(0, (taskbar.Height - height) / 2);
            if (hideInFullscreen && IsFullscreenForeground(monitor, taskbar))
            {
                continue;
            }

            placements.Add(new TaskbarPlacement(
                shell,
                screen.DeviceName,
                screen.Primary,
                candidateLeft,
                top,
                width,
                height,
                dpi));
        }

        if (string.Equals(monitorSelection, "All", StringComparison.OrdinalIgnoreCase))
        {
            return placements;
        }

        var selected = string.Equals(monitorSelection, "Primary", StringComparison.OrdinalIgnoreCase)
            ? placements.Where(item => item.IsPrimaryMonitor).ToList()
            : placements.Where(item => string.Equals(item.MonitorDeviceName, monitorSelection, StringComparison.OrdinalIgnoreCase)).ToList();

        if (selected.Count > 0)
        {
            return selected.Take(1).ToList();
        }

        // A previously selected display may have been disconnected or may not expose a taskbar.
        // Fall back deterministically to the primary taskbar instead of leaving a stale widget behind.
        return placements.Where(item => item.IsPrimaryMonitor).Take(1).ToList();
    }

    public static bool IsTaskbarAlive(IntPtr handle) => handle != IntPtr.Zero && Win32.IsWindowVisible(handle);

    private static bool IsFullscreenForeground(IntPtr monitor, Win32.Rect taskbar)
    {
        var foreground = Win32.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        var foregroundClass = GetClassName(foreground);
        if (foregroundClass is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Windows.UI.Core.CoreWindow" or
            "Windows.UI.Composition.DesktopWindowContentBridge" or "XamlExplorerHostIslandWindow")
        {
            return false;
        }

        try
        {
            _ = Win32.GetWindowThreadProcessId(foreground, out var processId);
            if (processId != 0)
            {
                using var process = Process.GetProcessById(unchecked((int)processId));
                if (process.ProcessName is "explorer" or "StartMenuExperienceHost" or "ShellExperienceHost" or "SearchHost" or "ShellHost")
                {
                    return false;
                }
            }
        }
        catch
        {
            // If process inspection is unavailable, continue with the rectangle check.
        }

        if (!Win32.GetWindowRect(foreground, out var rect) || rect.IsEmpty)
        {
            return false;
        }

        var info = new Win32.MonitorInfo { CbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.MonitorInfo>() };
        if (!Win32.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        return rect.Left <= info.Monitor.Left && rect.Top <= info.Monitor.Top &&
               rect.Right >= info.Monitor.Right && rect.Bottom >= info.Monitor.Bottom &&
               !rect.Equals(taskbar);
    }

    private static Win32.Rect? FindChildRect(IntPtr shell, string className)
    {
        var child = FindDescendant(shell, className);
        return child != IntPtr.Zero && Win32.GetWindowRect(child, out var rect) && !rect.IsEmpty ? rect : null;
    }

    private static IntPtr FindDescendant(IntPtr parent, string className)
    {
        var child = Win32.FindWindowEx(parent, IntPtr.Zero, className, null);
        if (child != IntPtr.Zero)
        {
            return child;
        }

        for (var current = Win32.FindWindowEx(parent, IntPtr.Zero, null, null); current != IntPtr.Zero; current = Win32.FindWindowEx(parent, current, null, null))
        {
            var nested = FindDescendant(current, className);
            if (nested != IntPtr.Zero)
            {
                return nested;
            }
        }

        return IntPtr.Zero;
    }

    private static string GetClassName(IntPtr handle)
    {
        var builder = new System.Text.StringBuilder(256);
        return Win32.GetClassName(handle, builder, builder.Capacity) > 0 ? builder.ToString() : string.Empty;
    }

    private static uint GetDpi(IntPtr shell, IntPtr monitor)
    {
        try
        {
            var windowDpi = Win32.GetDpiForWindow(shell);
            if (windowDpi > 0)
            {
                return windowDpi;
            }

            return Win32.GetDpiForMonitor(monitor, Win32.MonitorDpiType.Effective, out var dpiX, out _) == 0 && dpiX > 0 ? dpiX : 96;
        }
        catch (DllNotFoundException)
        {
            return 96;
        }
        catch (EntryPointNotFoundException)
        {
            return 96;
        }
    }
}
