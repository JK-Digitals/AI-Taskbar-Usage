#nullable enable
using Microsoft.Win32;

namespace AiTaskbarUsage;

public static class StartupManager
{
    private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "AI Taskbar Usage";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex)
        {
            SafeLogger.Write("startup", "Could not read auto-start setting: " + ex.GetType().Name);
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
            {
                var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Process path unavailable.");
                key.SetValue(ValueName, '"' + exe + '"');
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }
        }
        catch (Exception ex)
        {
            SafeLogger.Write("startup", "Could not update auto-start setting: " + ex.GetType().Name);
        }
    }
}
