using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RobloxInstanceOptimizer.App;

public sealed class RobloxInstanceRow : INotifyPropertyChanged
{
    private long _workingSetBytes;
    private int _memoryLimitMb = 1024;
    private int _coreCount = 1;
    private int _startCore;
    private ProcessPriorityClass _priorityClass = ProcessPriorityClass.BelowNormal;
    private string _affinityText = "-";
    private string _status = "Pret";

    public int ProcessId { get; init; }

    public string DisplayName { get; set; } = "";

    public long WorkingSetBytes
    {
        get => _workingSetBytes;
        set
        {
            if (SetField(ref _workingSetBytes, value))
            {
                OnPropertyChanged(nameof(MemoryText));
            }
        }
    }

    public string MemoryText => $"{WorkingSetBytes / 1024d / 1024d:0} Mo";

    public int MemoryLimitMb
    {
        get => _memoryLimitMb;
        set => SetField(ref _memoryLimitMb, Math.Max(0, value));
    }

    public int CoreCount
    {
        get => _coreCount;
        set => SetField(ref _coreCount, Math.Max(1, value));
    }

    public int StartCore
    {
        get => _startCore;
        set => SetField(ref _startCore, Math.Max(0, value));
    }

    public ProcessPriorityClass PriorityClass
    {
        get => _priorityClass;
        set => SetField(ref _priorityClass, value);
    }

    public string AffinityText
    {
        get => _affinityText;
        set => SetField(ref _affinityText, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
