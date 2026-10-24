using System.IO;
using KayoneZSoundpad.Models;

namespace KayoneZSoundpad.Services;

/// <summary>Ayarları yükler/kaydeder; eksik alanlar varsayılanla tamamlanır.</summary>
public class SettingsService
{
    private readonly string _path;
    public AppSettings Settings { get; private set; }

    public SettingsService(string? path = null)
    {
        _path = path ?? AppPaths.SettingsPath;
        Settings = JsonStore.Load<AppSettings>(_path) ?? new AppSettings();
        Sanitize();
    }

    private void Sanitize()
    {
        var s = Settings;
        s.MicVolume = Math.Clamp(s.MicVolume, 0f, 1.5f);
        s.FxVolume = Math.Clamp(s.FxVolume, 0f, 1.5f);
        s.DuckFactor = Math.Clamp(s.DuckFactor, 0f, 1f);
        s.MaxCacheMb = Math.Clamp(s.MaxCacheMb, 32, 2048);
        s.EngineLatencyMs = Math.Clamp(s.EngineLatencyMs, 20, 200);
    }

    public void Save()
    {
        Sanitize();
        JsonStore.Save(_path, Settings);
    }
}
