using System.Diagnostics;
using System.Net.Http;
using System.Reflection;

namespace RobloxInstanceOptimizer.App;

internal static class UpdateService
{
    private const string VersionUrl = "https://raw.githubusercontent.com/6usss/roblox-opti/main/VERSION";
    private const string InstallCommand = "irm https://raw.githubusercontent.com/6usss/roblox-opti/main/install.ps1 | iex";

    public static async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        var remoteText = await client.GetStringAsync(VersionUrl, cancellationToken);
        var remoteVersion = Version.Parse(remoteText.Trim());
        var currentVersion = GetCurrentVersion();

        return new UpdateCheckResult(currentVersion, remoteVersion, remoteVersion > currentVersion);
    }

    public static void StartUpdater()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{InstallCommand}\"",
            UseShellExecute = true,
            Verb = "runas"
        };

        Process.Start(startInfo);
    }

    private static Version GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null
            ? new Version(0, 0, 0)
            : new Version(version.Major, version.Minor, version.Build);
    }
}

internal sealed record UpdateCheckResult(Version CurrentVersion, Version RemoteVersion, bool UpdateAvailable);
