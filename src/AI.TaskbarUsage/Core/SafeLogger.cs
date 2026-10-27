#nullable enable
using System.Text.RegularExpressions;

namespace AiTaskbarUsage;

public static class SafeLogger
{
    private static readonly object Gate = new();
    private static readonly Regex SecretPattern = new(
        @"(?i)(bearer\s+|oauth[_-]?token\s*[=:]\s*|access[_-]?token\s*[=:]\s*|refresh[_-]?token\s*[=:]\s*)[^\s,;]+",
        RegexOptions.Compiled);
    private static readonly Regex JsonSecretPattern = new(
        @"(?i)(""(?:(?:access_token|refresh_token|id_token|api_key|token))""\s*:\s*"")[^""]*(""?)(\s*[,}])",
        RegexOptions.Compiled);

    public static string LogDirectory
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "AI Taskbar Usage", "logs");
        }
    }

    public static string LogFile => Path.Combine(LogDirectory, "app.log");

    public static void Write(string area, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var safe = SecretPattern.Replace(message, "[redacted]");
            safe = JsonSecretPattern.Replace(safe, "$1[redacted]$3");
            var line = $"{DateTimeOffset.Now:O} [{area}] {safe}{Environment.NewLine}";
            lock (Gate)
            {
                File.AppendAllText(LogFile, line);
            }
        }
        catch
        {
            // Logging must never affect the refresh loop or the taskbar widget.
        }
    }

}
