using System.IO;
using Xunit;
using KayoneZSoundpad.Features;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Tests;

public class FeatureServiceTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly string _settingsPath;

    public FeatureServiceTests()
    {
        _settingsPath = Path.Combine(_dir.Path, "settings.json");
    }

    public void Dispose() => _dir.Dispose();

    private (SettingsService Settings, FeatureService Features) Create()
    {
        var settings = new SettingsService(_settingsPath);
        return (settings, new FeatureService(settings));
    }

    [Fact]
    public void Defaults_MatchCatalog()
    {
        var (_, features) = Create();

        Assert.True(features.IsEnabled(FeatureId.LiveScenes));
        Assert.True(features.IsEnabled(FeatureId.AudioEffectsStudio));
        Assert.True(features.IsEnabled(FeatureId.SmartSoundSearch));
        Assert.True(features.IsEnabled(FeatureId.AdvancedAudioRouting));
        Assert.True(features.IsEnabled(FeatureId.MacroSoundboard));
        Assert.True(features.IsEnabled(FeatureId.MiniMode));
        Assert.False(features.IsEnabled(FeatureId.AiSoundAssistant));
        Assert.True(features.IsEnabled(FeatureId.CommunitySoundPacks));
    }

    [Fact]
    public void SetEnabled_IsPersistedAndReloaded()
    {
        var (_, features) = Create();
        features.SetEnabled(FeatureId.SmartSoundSearch, false);
        features.SetEnabled(FeatureId.AiSoundAssistant, true);

        var (_, reloaded) = Create();

        Assert.False(reloaded.IsEnabled(FeatureId.SmartSoundSearch));
        Assert.True(reloaded.IsEnabled(FeatureId.AiSoundAssistant));
    }

    [Fact]
    public void SetEnabled_RaisesFeatureChangedOnlyOnRealChange()
    {
        var (_, features) = Create();
        var changes = new List<FeatureChangedEventArgs>();
        features.FeatureChanged += (_, e) => changes.Add(e);

        features.SetEnabled(FeatureId.MiniMode, false);
        features.SetEnabled(FeatureId.MiniMode, false);

        var change = Assert.Single(changes);
        Assert.Equal(FeatureId.MiniMode, change.Feature);
        Assert.False(change.Enabled);
    }

    [Fact]
    public void OldSettingsWithoutFeatures_UsesDefaults()
    {
        File.WriteAllText(_settingsPath, "{\"theme\":\"light\",\"language\":\"tr\"}");
        var (_, features) = Create();

        Assert.True(features.IsEnabled(FeatureId.LiveScenes));
        Assert.False(features.IsEnabled(FeatureId.AiSoundAssistant));
    }

    [Fact]
    public void NullFeaturesInJson_IsNormalized()
    {
        File.WriteAllText(_settingsPath, "{\"features\":null}");
        var (settings, features) = Create();

        Assert.NotNull(settings.Settings.Features);
        Assert.True(features.IsEnabled(FeatureId.LiveScenes));
    }

    [Fact]
    public void UnknownFeatureKeys_ArePreserved()
    {
        File.WriteAllText(_settingsPath,
            "{\"features\":{\"FutureFeature\":true,\"SmartSoundSearch\":false}}");
        var (settings, features) = Create();

        Assert.False(features.IsEnabled(FeatureId.SmartSoundSearch));
        features.SetEnabled(FeatureId.MiniMode, false);
        settings.Save();

        var json = File.ReadAllText(_settingsPath);
        Assert.Contains("FutureFeature", json);
        Assert.Contains("SmartSoundSearch", json);
    }

    [Fact]
    public void Catalog_HasUniqueIdsAndKeys()
    {
        var ids = FeatureCatalog.All.Select(d => d.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.All(FeatureCatalog.All, d =>
        {
            Assert.True(LocalizationService.HasKey(d.NameKey, "tr"));
            Assert.True(LocalizationService.HasKey(d.NameKey, "en"));
            Assert.True(LocalizationService.HasKey(d.DescriptionKey, "tr"));
            Assert.True(LocalizationService.HasKey(d.DescriptionKey, "en"));
        });
    }
}
