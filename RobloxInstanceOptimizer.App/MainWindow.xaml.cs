using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;

namespace RobloxInstanceOptimizer.App;

public partial class MainWindow : Window
{
    private const string RobloxProcessName = "RobloxPlayerBeta";
    private static readonly TimeSpan MinimumTrimInterval = TimeSpan.FromSeconds(20);

    private readonly DispatcherTimer _refreshTimer;
    private readonly RobloxProcessOptimizer _optimizer = new();
    private AppSettings _settings;

    public ObservableCollection<RobloxInstanceRow> Instances { get; } = new();

    public IReadOnlyList<ProcessPriorityClass> PriorityOptions { get; } =
    [
        ProcessPriorityClass.Idle,
        ProcessPriorityClass.BelowNormal,
        ProcessPriorityClass.Normal,
        ProcessPriorityClass.AboveNormal,
        ProcessPriorityClass.High
    ];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _settings = AppSettings.Load();
        LoadSettingsIntoControls();

        AdminStatusText.Text = IsRunningAsAdministrator()
            ? "Administrator: yes"
            : "Administrator: no";

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _refreshTimer.Tick += (_, _) =>
        {
            if (AutoRefreshCheckBox.IsChecked == true)
            {
                RefreshInstances();
                RunAutoTrim();
            }
        };
        _refreshTimer.Start();

        RefreshInstances();
    }

    protected override void OnClosed(EventArgs e)
    {
        _optimizer.Dispose();
        base.OnClosed(e);
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshInstances();
    }

    private void ApplySelectedButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedRows = InstancesGrid.SelectedItems.OfType<RobloxInstanceRow>().ToArray();
        ApplyRows(selectedRows.Length == 0 ? Instances : selectedRows);
    }

    private void ApplyAllButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyRows(Instances);
    }

    private void SaveDefaultsButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromControls();
        StatusText.Text = $"Defaults saved: {AppSettings.SettingsPath}";
    }

    private void ApplyDefaultsToAllButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromControls();
        foreach (var row in Instances)
        {
            ApplyDefaults(row);
        }

        RecalculateDistributedCores();
        ApplyRows(Instances);
    }

    private void RefreshInstances()
    {
        var existingByPid = Instances.ToDictionary(instance => instance.ProcessId);
        var processes = Process.GetProcessesByName(RobloxProcessName)
            .OrderBy(process => process.Id)
            .ToArray();
        var liveIds = processes.Select(process => process.Id).ToHashSet();
        var hasNewInstances = false;

        foreach (var row in Instances.Where(row => !liveIds.Contains(row.ProcessId)).ToArray())
        {
            Instances.Remove(row);
        }

        foreach (var process in processes)
        {
            using (process)
            {
                if (!existingByPid.TryGetValue(process.Id, out var row))
                {
                    row = new RobloxInstanceRow
                    {
                        ProcessId = process.Id,
                        DisplayName = BuildDisplayName(process),
                    };
                    ApplyDefaults(row);
                    Instances.Add(row);
                    UpdateRuntimeInfo(process, row);
                    hasNewInstances = true;
                }

                UpdateRuntimeInfo(process, row);
            }
        }

        RecalculateDistributedCores();

        if (hasNewInstances && _settings.AutoApplyDefaultsToNewInstances)
        {
            ApplyRows(Instances, refreshAfterApply: false);
        }

        _optimizer.PruneDeadJobs(liveIds);
        StatusText.Text = Instances.Count == 0
            ? "No Roblox instances detected."
            : $"{Instances.Count} Roblox instance(s) detected.";
    }

    private void ApplyRows(IEnumerable<RobloxInstanceRow> rows, bool refreshAfterApply = true)
    {
        RecalculateDistributedCores();
        var applied = 0;
        foreach (var row in rows.ToArray())
        {
            try
            {
                _optimizer.Apply(row);
                row.Status = $"CPU applied: start {row.StartCore}, {row.CoreCount} core(s). Cleanup > {row.MemoryLimitMb} MB.";
                applied++;
            }
            catch (Exception ex)
            {
                row.Status = ex.Message;
            }
        }

        if (refreshAfterApply)
        {
            RefreshInstances();
        }

        StatusText.Text = $"Settings applied to {applied} instance(s).";
    }

    private static void UpdateRuntimeInfo(Process process, RobloxInstanceRow row)
    {
        try
        {
            process.Refresh();
            row.WorkingSetBytes = process.WorkingSet64;
            row.DisplayName = BuildDisplayName(process);
            row.AffinityText = FormatAffinity(process.ProcessorAffinity);
        }
        catch (Exception ex)
        {
            row.Status = ex.Message;
        }
    }

    private static string BuildDisplayName(Process process)
    {
        var title = process.MainWindowTitle;
        return string.IsNullOrWhiteSpace(title)
            ? $"{RobloxProcessName}.exe"
            : title;
    }

    private static string FormatAffinity(nint affinity)
    {
        var mask = (ulong)affinity;
        var cores = new List<int>();

        for (var i = 0; i < Math.Min(Environment.ProcessorCount, 64); i++)
        {
            if ((mask & (1UL << i)) != 0)
            {
                cores.Add(i);
            }
        }

        return cores.Count == 0 ? "-" : string.Join(", ", cores);
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private void RunAutoTrim()
    {
        if (AutoTrimCheckBox.IsChecked != true)
        {
            return;
        }

        foreach (var row in Instances.ToArray())
        {
            try
            {
                if (_optimizer.TrimMemoryIfNeeded(row, MinimumTrimInterval))
                {
                    row.Status = $"RAM cleaned at {DateTime.Now:HH:mm:ss}.";
                }
            }
            catch (Exception ex)
            {
                row.Status = ex.Message;
            }
        }
    }

    private void ApplyDefaults(RobloxInstanceRow row)
    {
        row.MemoryLimitMb = _settings.DefaultMemoryThresholdMb;
        row.CoreCount = _settings.DefaultCoreCount;
        row.PriorityClass = _settings.DefaultPriorityClass;
        row.StartCore = 0;
        row.Status = "Defaults loaded.";
    }

    private void RecalculateDistributedCores()
    {
        if (!_settings.AutoDistributeCpuCores)
        {
            return;
        }

        var processorCount = Math.Min(Environment.ProcessorCount, nint.Size * 8);
        if (processorCount <= 0)
        {
            return;
        }

        var nextCore = 0;
        foreach (var row in Instances.OrderBy(instance => instance.ProcessId))
        {
            row.StartCore = nextCore;
            var coreCount = Math.Clamp(row.CoreCount, 1, processorCount);
            nextCore = (nextCore + coreCount) % processorCount;
        }
    }

    private void LoadSettingsIntoControls()
    {
        DefaultMemoryThresholdTextBox.Text = _settings.DefaultMemoryThresholdMb.ToString();
        DefaultCoreCountTextBox.Text = _settings.DefaultCoreCount.ToString();
        DefaultPriorityComboBox.SelectedItem = _settings.DefaultPriorityClass;
        AutoRefreshCheckBox.IsChecked = _settings.AutoRefresh;
        AutoTrimCheckBox.IsChecked = _settings.AutoTrim;
        AutoApplyDefaultsCheckBox.IsChecked = _settings.AutoApplyDefaultsToNewInstances;
        AutoDistributeCoresCheckBox.IsChecked = _settings.AutoDistributeCpuCores;
    }

    private void SaveSettingsFromControls()
    {
        _settings.DefaultMemoryThresholdMb = ParseIntOrDefault(
            DefaultMemoryThresholdTextBox.Text,
            _settings.DefaultMemoryThresholdMb,
            minimum: 0,
            maximum: 65536);
        _settings.DefaultCoreCount = ParseIntOrDefault(
            DefaultCoreCountTextBox.Text,
            _settings.DefaultCoreCount,
            minimum: 1,
            maximum: Environment.ProcessorCount);
        _settings.DefaultPriorityClass = DefaultPriorityComboBox.SelectedItem is ProcessPriorityClass priority
            ? priority
            : ProcessPriorityClass.BelowNormal;
        _settings.AutoRefresh = AutoRefreshCheckBox.IsChecked == true;
        _settings.AutoTrim = AutoTrimCheckBox.IsChecked == true;
        _settings.AutoApplyDefaultsToNewInstances = AutoApplyDefaultsCheckBox.IsChecked == true;
        _settings.AutoDistributeCpuCores = AutoDistributeCoresCheckBox.IsChecked == true;
        _settings.Save();

        LoadSettingsIntoControls();
    }

    private static int ParseIntOrDefault(string text, int fallback, int minimum, int maximum)
    {
        return int.TryParse(text, out var value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;
    }
}
