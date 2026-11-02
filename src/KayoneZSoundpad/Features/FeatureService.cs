using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Features;

public sealed record FeatureChangedEventArgs(FeatureId Feature, bool Enabled);

/// <summary>
/// Ayar sözlüğü üzerinden özellik durumunu yönetir. Kayıtlı değer yoksa katalog
/// varsayılanı kullanılır; bilinmeyen anahtarlar korunur (ileriye dönük uyumluluk).
/// </summary>
public sealed class FeatureService
{
    private readonly SettingsService _settings;

    public FeatureService(SettingsService settings)
    {
        _settings = settings;
        _settings.Settings.Features ??= new Dictionary<string, bool>();
    }

    public event EventHandler<FeatureChangedEventArgs>? FeatureChanged;

    public IReadOnlyList<FeatureDefinition> Definitions => FeatureCatalog.All;

    public bool IsEnabled(FeatureId id)
    {
        var key = FeatureCatalog.Key(id);
        if (_settings.Settings.Features.TryGetValue(key, out var enabled))
            return enabled;
        return FeatureCatalog.Get(id).DefaultEnabled;
    }

    public bool AreDependenciesEnabled(FeatureId id)
        => FeatureCatalog.Get(id).RequiredFeatures.All(IsEnabled);

    /// <summary>
    /// Değeri kalıcılaştırır. Etkinleştirilen özelliğin bağımlılıkları kapalıysa
    /// bağımlılıklar da açılır; böylece yarım çalışan bir özellik oluşmaz.
    /// </summary>
    public void SetEnabled(FeatureId id, bool enabled)
    {
        if (enabled)
        {
            foreach (var dependency in FeatureCatalog.Get(id).RequiredFeatures)
                SetEnabled(dependency, true);
        }

        if (IsEnabled(id) == enabled)
            return;

        _settings.Settings.Features[FeatureCatalog.Key(id)] = enabled;
        _settings.Save();
        FeatureChanged?.Invoke(this, new FeatureChangedEventArgs(id, enabled));
    }

    /// <summary>Test/UI için tüm katalog tanımlarını döner.</summary>
    public IEnumerable<(FeatureDefinition Definition, bool Enabled)> Snapshot()
        => Definitions.Select(d => (d, IsEnabled(d.Id)));
}
