using System.IO;
using System.IO.Compression;
using System.Text.Json.Serialization;
using KayoneZSoundpad.Models;

namespace KayoneZSoundpad.Services;

/// <summary>
/// Kütüphane yedekleme: tüm sesler + library.json tek .zip içinde.
/// Ayarlar makineye özel aygıt id'leri içerdiğinden aktarılmaz.
/// </summary>
public class ImportExportService
{
    private readonly LibraryService _library;
    private readonly SettingsService _settings;

    public ImportExportService(LibraryService library, SettingsService settings)
    {
        _library = library;
        _settings = settings;
    }

    private class ExportSound
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("file")] public string File { get; set; } = "";
        [JsonPropertyName("categoryId")] public Guid? CategoryId { get; set; }
        [JsonPropertyName("color")] public string? Color { get; set; }
        [JsonPropertyName("icon")] public string? Icon { get; set; }
        [JsonPropertyName("hotkeyVk")] public int? HotkeyVk { get; set; }
        [JsonPropertyName("hotkeyMods")] public int? HotkeyMods { get; set; }
        [JsonPropertyName("volume")] public float Volume { get; set; } = 1f;
        [JsonPropertyName("isMuted")] public bool IsMuted { get; set; }
        [JsonPropertyName("behavior")] public int Behavior { get; set; }
        [JsonPropertyName("holdToPlay")] public bool HoldToPlay { get; set; }
        [JsonPropertyName("favorite")] public bool Favorite { get; set; }
        [JsonPropertyName("duration")] public double Duration { get; set; }
    }

    private class ExportData
    {
        [JsonPropertyName("app")] public string App { get; set; } = "Voxora";
        [JsonPropertyName("version")] public int Version { get; set; } = 1;
        [JsonPropertyName("categories")] public List<Category> Categories { get; set; } = new();
        [JsonPropertyName("sounds")] public List<ExportSound> Sounds { get; set; } = new();
    }

    public int Export(string zipPath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kayonez-export-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(tempDir);
            var filesDir = Path.Combine(tempDir, "files");
            Directory.CreateDirectory(filesDir);

            var export = new ExportData { Categories = _library.Data.Categories };
            foreach (var s in _library.Data.Sounds)
            {
                if (!File.Exists(s.FilePath)) continue; // bozuk kayıtları atla
                var fileName = s.Id + Path.GetExtension(s.FilePath);
                File.Copy(s.FilePath, Path.Combine(filesDir, fileName));
                export.Sounds.Add(new ExportSound
                {
                    Name = s.Name,
                    File = "files/" + fileName,
                    CategoryId = s.CategoryId,
                    Color = s.Color,
                    Icon = s.Icon,
                    HotkeyVk = s.Hotkey?.VirtualKey,
                    HotkeyMods = s.Hotkey != null ? (int)s.Hotkey.Modifiers : null,
                    Volume = s.Volume,
                    IsMuted = s.IsMuted,
                    Behavior = (int)s.Behavior,
                    HoldToPlay = s.HoldToPlay,
                    Favorite = s.Favorite,
                    Duration = s.DurationSeconds
                });
            }
            JsonStore.Save(Path.Combine(tempDir, "library.json"), export);

            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return export.Sounds.Count;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>Zip'ten içe aktarır; eklenen ses sayısını döner.</summary>
    public int Import(string zipPath, bool copyToLibrary)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kayonez-import-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            // Safe extraction with zip slip protection
            Directory.CreateDirectory(tempDir);
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    // Prevent directory traversal
                    var destinationPath = Path.GetFullPath(Path.Combine(tempDir, entry.FullName));
                    var tempDirFull = Path.GetFullPath(tempDir);
                    if (!destinationPath.StartsWith(tempDirFull, StringComparison.OrdinalIgnoreCase))
                        continue; // skip malicious entry
                    // Ensure directory exists
                    var dir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    // Extract
                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }

            var data = JsonStore.Load<ExportData>(Path.Combine(tempDir, "library.json"));
            if (data == null) throw new InvalidDataException("Geçersiz yedek dosyası (library.json yok).");

            // kategorileri taşı (aynı id varsa dokunma)
            foreach (var c in data.Categories)
                if (_library.Data.Categories.All(x => x.Id != c.Id))
                    _library.Data.Categories.Add(c);

            int added = 0;
            foreach (var es in data.Sounds)
            {
                // Resolve file path safely
                var requestedPath = Path.Combine(tempDir, es.File.Replace('/', Path.DirectorySeparatorChar));
                var resolvedPath = Path.GetFullPath(requestedPath);
                var tempDirFull = Path.GetFullPath(tempDir);
                if (!resolvedPath.StartsWith(tempDirFull, StringComparison.OrdinalIgnoreCase))
                    continue; // path traversal attempt
                if (!File.Exists(resolvedPath)) continue;
                if (!SoundFormatSupported(resolvedPath)) continue;

                var item = _library.AddSound(resolvedPath, es.CategoryId, copyToLibrary,
                    color: es.Color, icon: es.Icon, displayName: es.Name);
                item.Hotkey = es.HotkeyVk is int vk && vk != 0
                    ? new HotkeyGesture { VirtualKey = vk, Modifiers = (HotkeyModifiers)(es.HotkeyMods ?? 0) }
                    : null;
                item.Volume = es.Volume;
                item.IsMuted = es.IsMuted;
                item.Behavior = (PlaybackBehavior)es.Behavior;
                item.HoldToPlay = es.HoldToPlay;
                item.Favorite = es.Favorite;
                item.DurationSeconds = es.Duration;
                added++;
            }
            _library.Save();
            return added;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static bool SoundFormatSupported(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a" or ".aac" or ".wma" or ".opus";
    }
}
