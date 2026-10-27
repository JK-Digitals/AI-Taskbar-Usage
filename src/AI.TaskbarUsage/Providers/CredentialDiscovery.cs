#nullable enable
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace AiTaskbarUsage;

public static class CredentialDiscovery
{
    private static readonly Lazy<string> ClaudeUserAgent = new(ResolveClaudeUserAgent);

    public static string GetClaudeUserAgent() => ClaudeUserAgent.Value;

    public static bool TryFindClaude(out LocalCredential credential)
    {
        foreach (var path in ClaudeCredentialPaths())
        {
            if (TryRead(path, CredentialParser.TryParseClaude, out credential))
            {
                return true;
            }
        }

        var environmentToken = Environment.GetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN");
        if (!string.IsNullOrWhiteSpace(environmentToken))
        {
            credential = new LocalCredential(environmentToken, null, null);
            return true;
        }

        credential = default!;
        return false;
    }

    public static bool TryFindCodex(out LocalCredential credential)
    {
        foreach (var path in CodexCredentialPaths())
        {
            if (TryRead(path, CredentialParser.TryParseCodex, out credential))
            {
                return true;
            }
        }

        credential = default!;
        return false;
    }

    public static IEnumerable<string> ClaudeCredentialPaths()
    {
        var configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        if (!string.IsNullOrWhiteSpace(configDir))
        {
            yield return Path.Combine(configDir, ".credentials.json");
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            yield return Path.Combine(profile, ".claude", ".credentials.json");
        }

        foreach (var path in WslCredentialPaths(".claude", ".credentials.json"))
        {
            yield return path;
        }
    }

    public static IEnumerable<string> CodexCredentialPaths()
    {
        var configured = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            yield return Path.Combine(configured, "auth.json");
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            yield return Path.Combine(profile, ".codex", "auth.json");
        }
    }

    private static IEnumerable<string> WslCredentialPaths(string directory, string file)
    {
        foreach (var distro in InstalledWslDistros())
        {
            var root = $"\\\\wsl$\\{distro}";
            var home = Path.Combine(root, "home");
            IReadOnlyList<string> users;
            try
            {
                users = Directory.Exists(home) ? Directory.GetDirectories(home) : [];
            }
            catch
            {
                users = [];
            }

            foreach (var user in users)
            {
                yield return Path.Combine(user, directory, file);
            }

            yield return Path.Combine(root, "root", directory, file);
        }
    }

    private static List<string> InstalledWslDistros()
    {
        var distros = new List<string>();
        var start = new ProcessStartInfo
        {
            FileName = "wsl.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            Arguments = "--list --quiet"
        };

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return distros;
            }

            if (!process.WaitForExit(1200))
            {
                try { process.Kill(); } catch { }
                return distros;
            }

            var output = process.StandardOutput.ReadToEnd();
            foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var distro = line.Trim().TrimStart('\uFEFF');
                if (distro.Length > 0 && distro.Length < 128 && !distro.Contains('\\'))
                {
                    distros.Add(distro);
                }
            }
        }
        catch
        {
            return distros;
        }

        return distros;
    }

    private static bool TryRead(string path, TryParse parser, out LocalCredential credential)
    {
        credential = default!;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > 1024 * 1024)
            {
                return false;
            }

            var json = File.ReadAllText(path);
            return parser(json, out credential);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private delegate bool TryParse(string json, out LocalCredential credential);

    private static string ResolveClaudeUserAgent()
    {
        var configured = Environment.GetEnvironmentVariable("CLAUDE_CODE_VERSION");
        var version = ExtractVersion(configured);
        if (version is not null)
        {
            return "claude-code/" + version;
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(profile, ".local", "bin", "claude.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Claude Code", "claude.exe")
        };

        foreach (var candidate in candidates)
        {
            try
            {
                if (File.Exists(candidate))
                {
                    version = ExtractVersion(FileVersionInfo.GetVersionInfo(candidate).ProductVersion);
                    if (version is not null)
                    {
                        return "claude-code/" + version;
                    }
                }
            }
            catch
            {
                // Continue with the CLI probe or safe fallback.
            }
        }

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = "--version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(start);
            if (process is not null && process.WaitForExit(1500))
            {
                version = ExtractVersion(process.StandardOutput.ReadToEnd());
                if (version is not null)
                {
                    return "claude-code/" + version;
                }
            }
        }
        catch
        {
            // Claude Code is optional; use a valid compatible UA when it is not on PATH.
        }

        return "claude-code/2.1.0";
    }

    private static string? ExtractVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = Regex.Match(value, @"\b\d+\.\d+(?:\.\d+)?\b");
        return match.Success ? match.Value : null;
    }
}
