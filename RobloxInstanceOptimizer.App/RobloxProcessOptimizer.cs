using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace RobloxInstanceOptimizer.App;

internal sealed class RobloxProcessOptimizer : IDisposable
{
    private readonly Dictionary<int, NativeJobObject> _jobsByProcessId = new();
    private readonly Dictionary<int, DateTimeOffset> _lastTrimByProcessId = new();

    public void Apply(RobloxInstanceRow row)
    {
        using var process = Process.GetProcessById(row.ProcessId);

        process.PriorityClass = row.PriorityClass;
        process.ProcessorAffinity = BuildAffinityMask(row.StartCore, row.CoreCount);
    }

    public bool TrimMemoryIfNeeded(RobloxInstanceRow row, TimeSpan minimumInterval)
    {
        if (row.MemoryLimitMb <= 0)
        {
            return false;
        }

        var currentMb = row.WorkingSetBytes / 1024d / 1024d;
        if (currentMb < row.MemoryLimitMb)
        {
            return false;
        }

        if (_lastTrimByProcessId.TryGetValue(row.ProcessId, out var lastTrim) &&
            DateTimeOffset.UtcNow - lastTrim < minimumInterval)
        {
            return false;
        }

        using var process = Process.GetProcessById(row.ProcessId);
        TrimWorkingSet(process);
        _lastTrimByProcessId[row.ProcessId] = DateTimeOffset.UtcNow;
        return true;
    }

    public void PruneDeadJobs(IReadOnlySet<int> liveProcessIds)
    {
        foreach (var processId in _lastTrimByProcessId.Keys.ToArray())
        {
            if (!liveProcessIds.Contains(processId))
            {
                _lastTrimByProcessId.Remove(processId);
            }
        }

        foreach (var processId in _jobsByProcessId.Keys.ToArray())
        {
            if (liveProcessIds.Contains(processId))
            {
                continue;
            }

            _jobsByProcessId[processId].Dispose();
            _jobsByProcessId.Remove(processId);
            _lastTrimByProcessId.Remove(processId);
        }
    }

    public void Dispose()
    {
        foreach (var job in _jobsByProcessId.Values)
        {
            job.Dispose();
        }

        _jobsByProcessId.Clear();
        _lastTrimByProcessId.Clear();
    }

    private void ApplyMemoryLimit(Process process, int limitMb)
    {
        var limitBytes = limitMb * 1024L * 1024L;

        if (!_jobsByProcessId.TryGetValue(process.Id, out var job))
        {
            job = NativeJobObject.Create();
            job.SetProcessMemoryLimit(limitBytes);
            job.AssignProcess(process.Handle);
            _jobsByProcessId[process.Id] = job;
            return;
        }

        job.SetProcessMemoryLimit(limitBytes);
    }

    private static nint BuildAffinityMask(int startCore, int requestedCoreCount)
    {
        var usableBits = nint.Size * 8;
        var processorCount = Math.Min(Environment.ProcessorCount, usableBits);
        var safeStartCore = Math.Clamp(startCore, 0, processorCount - 1);
        var coreCount = Math.Clamp(requestedCoreCount, 1, processorCount);
        ulong mask = 0;

        for (var offset = 0; offset < coreCount; offset++)
        {
            var core = (safeStartCore + offset) % processorCount;
            mask |= 1UL << core;
        }

        return (nint)mask;
    }

    private static void TrimWorkingSet(Process process)
    {
        if (!EmptyWorkingSet(process.Handle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "EmptyWorkingSet a echoue.");
        }
    }

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(nint hProcess);
}
