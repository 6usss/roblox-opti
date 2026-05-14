using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace RobloxInstanceOptimizer.App;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public int DefaultMemoryThresholdMb { get; set; } = 2048;

    public int DefaultCoreCount { get; set; } = Math.Min(2, Environment.ProcessorCount);

    public ProcessPriorityClass DefaultPriorityClass { get; set; } = ProcessPriorityClass.BelowNormal;

    public bool AutoRefresh { get; set; } = true;

    public bool AutoTrim { get; set; } = true;

    public bool AutoApplyDefaultsToNewInstances { get; set; } = true;

    public bool AutoDistributeCpuCores { get; set; } = true;

    public bool AutoUpdateOnLaunch { get; set; } = true;

    public bool StartupBoostEnabled { get; set; } = true;

    public int StartupBoostSeconds { get; set; } = 60;

    public int StartupBoostCoreCount { get; set; } = Math.Min(4, Environment.ProcessorCount);

    public ProcessPriorityClass StartupBoostPriorityClass { get; set; } = ProcessPriorityClass.Normal;

    public static string SettingsPath
    {
        get
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RobloxInstanceOptimizer");
            return Path.Combine(folder, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        var folder = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
