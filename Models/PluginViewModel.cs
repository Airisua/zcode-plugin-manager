using System.ComponentModel;
using System.Runtime.CompilerServices;
using ZCodePluginManager.Services;

namespace ZCodePluginManager.Models;

public sealed class PluginViewModel : INotifyPropertyChanged
{
    private PluginStatus _status;
    private bool _busy;

    public PluginViewModel(PluginDefinition definition, PluginStatus status)
    {
        Definition = definition;
        _status = status;
    }

    public PluginDefinition Definition { get; }

    public PluginStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));
        }
    }

    public bool Busy
    {
        get => _busy;
        set
        {
            _busy = value;
            OnPropertyChanged();
        }
    }

    public string StatusText => Status.StateText;
    public string StatusColor => Status.StateColor;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
