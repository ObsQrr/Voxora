using System.Text.Json.Serialization;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Models;

namespace KayoneZSoundpad.Models;

/// <summary>settings.json — kullanıcı tercihleri ve ses yönlendirme ayarları.</summary>
public class AppSettings
{
    // Görünüm
    [JsonPropertyName("theme")] public string Theme { get; set; } = "dark";
    [JsonPropertyName("language")] public string Language { get; set; } = "tr";
    /// <summary>İlk kurulum sihirbazı tamamlandı mı? (v2.0)</summary>
    [JsonPropertyName("onboardingCompleted")] public bool OnboardingCompleted { get; set; }
    [JsonPropertyName("rememberLastCategory")] public bool RememberLastCategory { get; set; } = true;
    [JsonPropertyName("lastCategoryId")] public Guid? LastCategoryId { get; set; }

    // Davranış
    [JsonPropertyName("closeToTray")] public bool CloseToTray { get; set; } = true;
    [JsonPropertyName("startMinimized")] public bool StartMinimized { get; set; } = false;
    [JsonPropertyName("autoStart")] public bool AutoStart { get; set; } = false;
    [JsonPropertyName("copyOnImport")] public bool CopyOnImport { get; set; } = true;
    [JsonPropertyName("defaultBehavior")] public PlaybackBehavior DefaultBehavior { get; set; } = PlaybackBehavior.Restart;
    [JsonPropertyName("suppressHotkeys")] public bool SuppressHotkeys { get; set; } = true;

    /// <summary>2.0 özellik anahtarları; eksik anahtar katalog varsayılanını kullanır.</summary>
    [JsonPropertyName("features")] public Dictionary<string, bool> Features { get; set; } = new();

    // Ses değiştirici (mikrofon hattı)
    [JsonPropertyName("voiceChanger")] public VoiceChangerSettings VoiceChanger { get; set; } = new();

    // Güncellemeler
    [JsonPropertyName("checkForUpdates")] public bool CheckForUpdates { get; set; } = true;
    [JsonPropertyName("lastUpdateCheckUtc")] public DateTime? LastUpdateCheckUtc { get; set; }

    // Ses aygıtları (MMDevice Id)
    [JsonPropertyName("outputDeviceId")] public string? OutputDeviceId { get; set; }
    [JsonPropertyName("micDeviceId")] public string? MicDeviceId { get; set; }
    [JsonPropertyName("monitorDeviceId")] public string? MonitorDeviceId { get; set; }

    // Miksaj
    [JsonPropertyName("micEnabled")] public bool MicEnabled { get; set; } = true;
    [JsonPropertyName("micVolume")] public float MicVolume { get; set; } = 1.0f;
    [JsonPropertyName("fxVolume")] public float FxVolume { get; set; } = 1.0f;
    [JsonPropertyName("monitorEnabled")] public bool MonitorEnabled { get; set; } = true;
    [JsonPropertyName("duckMicWhilePlaying")] public bool DuckMicWhilePlaying { get; set; } = false;
    [JsonPropertyName("duckFactor")] public float DuckFactor { get; set; } = 0.25f;

    // Kısayollar
    [JsonPropertyName("stopAllHotkey")] public HotkeyGesture? StopAllHotkey { get; set; }

    /// <summary>Oyun üstü daire menüsünü açar (varsayılan F1).</summary>
    [JsonPropertyName("radialMenuHotkey")]
    public HotkeyGesture? RadialMenuHotkey { get; set; } = new() { VirtualKey = 0x70 };

    // Motor
    [JsonPropertyName("maxCacheMb")] public int MaxCacheMb { get; set; } = 256;
    [JsonPropertyName("engineLatencyMs")] public int EngineLatencyMs { get; set; } = 50;
}
