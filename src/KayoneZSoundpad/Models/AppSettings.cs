using System.Text.Json.Serialization;
using KayoneZSoundpad.Models;

namespace KayoneZSoundpad.Models;

/// <summary>settings.json — kullanıcı tercihleri ve ses yönlendirme ayarları.</summary>
public class AppSettings
{
    // Görünüm
    [JsonPropertyName("theme")] public string Theme { get; set; } = "light";
    [JsonPropertyName("language")] public string Language { get; set; } = "tr";
    [JsonPropertyName("rememberLastCategory")] public bool RememberLastCategory { get; set; } = true;
    [JsonPropertyName("lastCategoryId")] public Guid? LastCategoryId { get; set; }

    // Davranış
    [JsonPropertyName("closeToTray")] public bool CloseToTray { get; set; } = true;
    [JsonPropertyName("startMinimized")] public bool StartMinimized { get; set; } = false;
    [JsonPropertyName("autoStart")] public bool AutoStart { get; set; } = false;
    [JsonPropertyName("copyOnImport")] public bool CopyOnImport { get; set; } = true;
    [JsonPropertyName("defaultBehavior")] public PlaybackBehavior DefaultBehavior { get; set; } = PlaybackBehavior.Restart;
    [JsonPropertyName("suppressHotkeys")] public bool SuppressHotkeys { get; set; } = true;

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

    // Motor
    [JsonPropertyName("maxCacheMb")] public int MaxCacheMb { get; set; } = 256;
    [JsonPropertyName("engineLatencyMs")] public int EngineLatencyMs { get; set; } = 50;
}
