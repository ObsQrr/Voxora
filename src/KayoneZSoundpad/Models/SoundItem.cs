using System.IO;
using System.Text.Json.Serialization;

namespace KayoneZSoundpad.Models;

public enum PlaybackBehavior
{
    Restart = 0,   // yeniden başlat
    Overlap = 1,   // üstüne çal
    Toggle = 2     // çalıyorsa durdur
}

/// <summary>Kütüphanedeki tek bir ses efekti kaydı.</summary>
public class SoundItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";
    public Guid? CategoryId { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public Models.HotkeyGesture? Hotkey { get; set; }
    public float Volume { get; set; } = 1.0f;          // 0..1.5
    public bool IsMuted { get; set; }                  // mikrofon/karta özel kısma
    public PlaybackBehavior Behavior { get; set; } = PlaybackBehavior.Restart;
    public bool HoldToPlay { get; set; }
    public bool Favorite { get; set; }
    public int Order { get; set; }
    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastPlayedUtc { get; set; }
    public double DurationSeconds { get; set; }

    [JsonIgnore]
    public bool FileExists => File.Exists(FilePath);
}

/// <summary>Ses kategorisi (klasör).</summary>
public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public int Order { get; set; }
}

/// <summary>Global "tüm sesleri durdur" dahil kısayol eylemleri.</summary>
public enum AppAction
{
    None = 0,
    StopAll = 1
}

