using System.ComponentModel;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Features;

/// <summary>
/// Ayarlar penceresindeki tek özellik satırı. Değişiklikler Save'e kadar bekletilir;
/// böylece İptal gerçek ayarları değiştirmez.
/// </summary>
public sealed class FeatureToggleViewModel : INotifyPropertyChanged
{
    private readonly FeatureService _service;
    private bool _enabled;

    public FeatureToggleViewModel(FeatureService service, FeatureDefinition definition)
    {
        _service = service;
        Definition = definition;
        _enabled = service.IsEnabled(definition.Id);
    }

    public FeatureDefinition Definition { get; }
    public FeatureId Id => Definition.Id;

    public bool IsEnabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            OnPropertyChanged(nameof(IsEnabled));
        }
    }

    public string Name => LocalizationService.T(Definition.NameKey);
    public string Description => LocalizationService.T(Definition.DescriptionKey);
    public bool ShowRestartHint => Definition.RequiresRestart;
    public string RestartHint => Definition.RequiresRestart
        ? LocalizationService.T("Feature_RestartRequired")
        : "";

    /// <summary>Modül henüz tamamlanmadıysa arayüzde dürüst bir rozet gösterilir.</summary>
    public bool ShowAvailability => Definition.Availability != FeatureAvailability.Available;
    public string AvailabilityText => Definition.Availability switch
    {
        FeatureAvailability.Beta => LocalizationService.T("FeatureStatus_Beta"),
        FeatureAvailability.ComingSoon => LocalizationService.T("FeatureStatus_ComingSoon"),
        _ => ""
    };

    public void Apply() => _service.SetEnabled(Definition.Id, _enabled);

    public void RefreshText()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(RestartHint));
        OnPropertyChanged(nameof(ShowRestartHint));
        OnPropertyChanged(nameof(AvailabilityText));
        OnPropertyChanged(nameof(ShowAvailability));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
