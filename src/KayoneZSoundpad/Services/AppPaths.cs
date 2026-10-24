using System.IO;
using KayoneZSoundpad.Models;

namespace KayoneZSoundpad.Services;

/// <summary>%APPDATA%\KayoneZSoundpad altındaki veri dizinini yönetir.</summary>
public static class AppPaths
{
    private static string? _override;

    public static string BaseDir
        => _override ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KayoneZSoundpad");

    /// <summary>Testler için veri dizinini yönlendirir.</summary>
    public static void SetOverride(string? dir) => _override = dir;

    public static string SettingsPath => Path.Combine(BaseDir, "settings.json");
    public static string LibraryPath => Path.Combine(BaseDir, "library.json");
    public static string SoundsDir => Path.Combine(BaseDir, "Sounds");

    public static void EnsureDirs() => Directory.CreateDirectory(BaseDir);
}
