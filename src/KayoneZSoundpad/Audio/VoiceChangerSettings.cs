using System.Text.Json.Serialization;

namespace KayoneZSoundpad.Audio;

public enum VoiceChangerPreset
{
    Natural,
    Chipmunk,
    Deep,
    Robot,
    Echo,
    Telephone,
    Custom
}

/// <summary>
/// Mikrofon hattına uygulanan gerçek zamanlı ses değiştirici ayarları.
/// DSP her zaman 48 kHz stereo float üzerinde çalışır.
/// </summary>
public sealed class VoiceChangerSettings
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("preset")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VoiceChangerPreset Preset { get; set; } = VoiceChangerPreset.Natural;
    [JsonPropertyName("pitchSemitones")] public int PitchSemitones { get; set; }
    [JsonPropertyName("robotMix")] public float RobotMix { get; set; }
    [JsonPropertyName("echoMix")] public float EchoMix { get; set; }
    [JsonPropertyName("echoDelayMs")] public int EchoDelayMs { get; set; } = 160;
    [JsonPropertyName("telephone")] public bool Telephone { get; set; }

    public VoiceChangerSettings Clone() => new()
    {
        Enabled = Enabled,
        Preset = Preset,
        PitchSemitones = PitchSemitones,
        RobotMix = RobotMix,
        EchoMix = EchoMix,
        EchoDelayMs = EchoDelayMs,
        Telephone = Telephone
    };

    public void Normalize()
    {
        PitchSemitones = Math.Clamp(PitchSemitones, -12, 12);
        RobotMix = Math.Clamp(RobotMix, 0f, 1f);
        EchoMix = Math.Clamp(EchoMix, 0f, 1f);
        EchoDelayMs = Math.Clamp(EchoDelayMs, 50, 500);
    }

    public bool ValueEquals(VoiceChangerSettings other)
        => other != null
           && Enabled == other.Enabled
           && Preset == other.Preset
           && PitchSemitones == other.PitchSemitones
           && Math.Abs(RobotMix - other.RobotMix) < 0.0001f
           && Math.Abs(EchoMix - other.EchoMix) < 0.0001f
           && EchoDelayMs == other.EchoDelayMs
           && Telephone == other.Telephone;

    public static VoiceChangerSettings ForPreset(VoiceChangerPreset preset)
    {
        var settings = new VoiceChangerSettings { Enabled = true, Preset = preset };
        switch (preset)
        {
            case VoiceChangerPreset.Chipmunk:
                settings.PitchSemitones = 12;
                break;
            case VoiceChangerPreset.Deep:
                settings.PitchSemitones = -6;
                break;
            case VoiceChangerPreset.Robot:
                settings.RobotMix = 0.75f;
                break;
            case VoiceChangerPreset.Echo:
                settings.EchoMix = 0.35f;
                settings.EchoDelayMs = 180;
                break;
            case VoiceChangerPreset.Telephone:
                settings.Telephone = true;
                break;
        }
        return settings;
    }
}
