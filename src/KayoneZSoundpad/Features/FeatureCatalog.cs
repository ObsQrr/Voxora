namespace KayoneZSoundpad.Features;

/// <summary>Bir modülün gerçek durumu — arayüzde "yakında" rozeti için.</summary>
public enum FeatureAvailability
{
    Available,
    Beta,
    ComingSoon
}

/// <summary>
/// Bir özelliğin meta verisi. Name/Description anahtarları LocalizationService'te bulunur.
/// </summary>
public sealed record FeatureDefinition(
    FeatureId Id,
    string NameKey,
    string DescriptionKey,
    bool DefaultEnabled,
    bool RequiresRestart = false,
    IReadOnlyList<FeatureId>? Dependencies = null,
    FeatureAvailability Availability = FeatureAvailability.Available)
{
    public IReadOnlyList<FeatureId> RequiredFeatures => Dependencies ?? Array.Empty<FeatureId>();
}

/// <summary>2.0 özellik kataloğu. Varsayılanlar kullanıcı ayarlarında yoksa geçerlidir.</summary>
public static class FeatureCatalog
{
    public static IReadOnlyList<FeatureDefinition> All { get; } = new[]
    {
        new FeatureDefinition(FeatureId.LiveScenes,
            "Feature_LiveScenes_Name", "Feature_LiveScenes_Desc", DefaultEnabled: true,
            Availability: FeatureAvailability.ComingSoon),
        new FeatureDefinition(FeatureId.AudioEffectsStudio,
            "Feature_AudioEffectsStudio_Name", "Feature_AudioEffectsStudio_Desc", DefaultEnabled: true,
            Availability: FeatureAvailability.ComingSoon),
        new FeatureDefinition(FeatureId.SmartSoundSearch,
            "Feature_SmartSoundSearch_Name", "Feature_SmartSoundSearch_Desc", DefaultEnabled: true,
            Availability: FeatureAvailability.Beta),
        new FeatureDefinition(FeatureId.AdvancedAudioRouting,
            "Feature_AdvancedAudioRouting_Name", "Feature_AdvancedAudioRouting_Desc", DefaultEnabled: true,
            Availability: FeatureAvailability.ComingSoon),
        new FeatureDefinition(FeatureId.MacroSoundboard,
            "Feature_MacroSoundboard_Name", "Feature_MacroSoundboard_Desc", DefaultEnabled: true,
            Availability: FeatureAvailability.ComingSoon),
        new FeatureDefinition(FeatureId.MiniMode,
            "Feature_MiniMode_Name", "Feature_MiniMode_Desc", DefaultEnabled: true,
            Availability: FeatureAvailability.ComingSoon),
        new FeatureDefinition(FeatureId.AiSoundAssistant,
            "Feature_AiSoundAssistant_Name", "Feature_AiSoundAssistant_Desc", DefaultEnabled: false,
            Availability: FeatureAvailability.ComingSoon),
        new FeatureDefinition(FeatureId.CommunitySoundPacks,
            "Feature_CommunitySoundPacks_Name", "Feature_CommunitySoundPacks_Desc", DefaultEnabled: true,
            Availability: FeatureAvailability.ComingSoon),
        new FeatureDefinition(FeatureId.VoiceChanger,
            "Feature_VoiceChanger_Name", "Feature_VoiceChanger_Desc", DefaultEnabled: true),
        new FeatureDefinition(FeatureId.RadialMenu,
            "Feature_RadialMenu_Name", "Feature_RadialMenu_Desc", DefaultEnabled: true)
    };

    private static readonly Dictionary<FeatureId, FeatureDefinition> ById = All.ToDictionary(d => d.Id);

    public static FeatureDefinition Get(FeatureId id)
        => ById.TryGetValue(id, out var definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown feature id");

    public static string Key(FeatureId id) => id.ToString();
}
