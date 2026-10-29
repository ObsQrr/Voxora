using System.IO;
using System.IO.Compression;
using System.Text.Json.Serialization;
using KayoneZSoundpad.Models;

namespace KayoneZSoundpad.Services;

/// <summary>
/// Kütüphane yedekleme: tüm sesler + library.json tek .zip içinde.
/// Ayarlar makineye özel aygıt id'leri içerdiğinden aktarılmaz.
///
/// Güvenlik: zip-slip, symlink, zip-bomb (boyut/adet sınırı) ve bozuk
/// metadata korumaları içerir.
/// </summary>
public class ImportExportService
{
    private const int MaxEntries = 5000;
    private const long MaxTotalUncompressed = 2L * 1024 * 1024 * 1024; // 2 GB
    private const long MaxSingleEntry = 512L * 1024 * 1024;            // 512 MB
    private const long MaxLibraryJson = 8L * 1024 * 1024;              // 8 MB

    private readonly LibraryService _library;

    public ImportExportService(LibraryService library, SettingsService settings)
    {
        _library = library;
        _ = settings; // ayarlar makineye özel olduğu için yedeğe dahil edilmez
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
        var tempDir = Path.Combine(Path.GetTempPath(), "voxora-export-" + Guid.NewGuid().ToString("N")[..8]);
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

            // Atomik yazma: önce geçici zip, sonra hedefin üzerine taşı.
            var dir = Path.GetDirectoryName(Path.GetFullPath(zipPath));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmpZip = zipPath + ".tmp";
            if (File.Exists(tmpZip)) File.Delete(tmpZip);
            ZipFile.CreateFromDirectory(tempDir, tmpZip, CompressionLevel.Optimal, includeBaseDirectory: false);
            File.Move(tmpZip, zipPath, overwrite: true);
            return export.Sounds.Count;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// Zip'ten içe aktarır; eklenen ses sayısını döner.
    /// Not: dosyalar her zaman kütüphaneye kopyalanır — geçici çıkarma dizini
    /// işlem sonunda silindiği için copyToLibrary=false güvenli değildir.
    /// </summary>
    public int Import(string zipPath, bool copyToLibrary = true)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "voxora-import-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(tempDir);
            ExtractSafely(zipPath, tempDir);

            var manifestPath = Path.Combine(tempDir, "library.json");
            var data = JsonStore.Load<ExportData>(manifestPath);
            if (data == null) throw new InvalidDataException(LocalizationService.T("Import_InvalidBackup"));
            if (data.Categories == null || data.Sounds == null)
                throw new InvalidDataException(LocalizationService.T("Import_InvalidBackup"));

            // kategorileri taşı (aynı id varsa dokunma)
            foreach (var c in data.Categories)
                if (c != null && _library.Data.Categories.All(x => x.Id != c.Id))
                    _library.Data.Categories.Add(c);

            var knownCategories = _library.Data.Categories.Select(c => c.Id).ToHashSet();
            int added = 0;
            foreach (var es in data.Sounds)
            {
                if (es == null || string.IsNullOrWhiteSpace(es.File)) continue;

                var resolvedPath = ResolveInside(tempDir, es.File);
                if (resolvedPath == null) continue; // path traversal / geçersiz
                if (!File.Exists(resolvedPath)) continue;
                if (!LibraryService.IsSupportedFormat(resolvedPath)) continue;

                // Geçici dizin silineceği için daima kütüphaneye kopyala.
                var item = _library.AddSound(resolvedPath, es.CategoryId, copyToLibrary: true,
                    color: SanitizeColor(es.Color), icon: SanitizeIcon(es.Icon),
                    displayName: SanitizeName(es.Name));
                item.CategoryId = es.CategoryId is Guid cid && knownCategories.Contains(cid) ? cid : null;
                item.Hotkey = es.HotkeyVk is int vk && vk > 0
                    ? new HotkeyGesture { VirtualKey = vk, Modifiers = (HotkeyModifiers)(es.HotkeyMods ?? 0) }
                    : null;
                item.Volume = Math.Clamp(es.Volume, 0f, 1.5f);
                item.IsMuted = es.IsMuted;
                item.Behavior = Enum.IsDefined(typeof(PlaybackBehavior), es.Behavior)
                    ? (PlaybackBehavior)es.Behavior
                    : PlaybackBehavior.Restart;
                item.HoldToPlay = es.HoldToPlay;
                item.Favorite = es.Favorite;
                item.DurationSeconds = Math.Max(0, es.Duration);
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

    /// <summary>
    /// Kullanıcının kendi ses dosyalarını (kütüphanedekiler + Sounds klasöründekiler)
    /// düz bir ZIP olarak dışa aktarır. library.json/settings.json eklenmez.
    /// </summary>
    public int ExportSoundFiles(string zipPath)
    {
        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string path)
        {
            if (!File.Exists(path) || !LibraryService.IsSupportedFormat(path)) return;
            if (seen.Add(Path.GetFullPath(path))) files.Add(path);
        }

        foreach (var s in _library.Data.Sounds) Add(s.FilePath);
        if (Directory.Exists(AppPaths.SoundsDir))
            foreach (var f in Directory.EnumerateFiles(AppPaths.SoundsDir, "*", SearchOption.TopDirectoryOnly))
                Add(f);

        if (files.Count == 0) return 0;

        var targetFull = Path.GetFullPath(zipPath);
        if (files.Any(f => string.Equals(Path.GetFullPath(f), targetFull, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(LocalizationService.T("Export_TargetIsSoundFile"));

        var dir = Path.GetDirectoryName(targetFull);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmpZip = zipPath + ".tmp";
        if (File.Exists(tmpZip)) File.Delete(tmpZip);
        try
        {
            using (var zip = ZipFile.Open(tmpZip, ZipArchiveMode.Create))
            {
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    var name = Path.GetFileName(file);
                    if (!usedNames.Add(name))
                    {
                        var stem = Path.GetFileNameWithoutExtension(file);
                        var ext = Path.GetExtension(file);
                        int n = 1;
                        do { name = $"{stem} ({n++}){ext}"; }
                        while (!usedNames.Add(name));
                    }
                    zip.CreateEntryFromFile(file, name, CompressionLevel.Optimal);
                }
            }
            File.Move(tmpZip, zipPath, overwrite: true);
            return files.Count;
        }
        finally
        {
            try { if (File.Exists(tmpZip)) File.Delete(tmpZip); } catch { }
        }
    }

    private static void ExtractSafely(string zipPath, string tempDir)
    {
        if (!File.Exists(zipPath)) throw new FileNotFoundException(zipPath);

        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > MaxEntries)
            throw new InvalidDataException(LocalizationService.T("Import_TooLarge"));

        long total = 0;
        foreach (var entry in archive.Entries)
        {
            // Dizin girişleri (adı "/" ile biter) çıkarılamaz; atla.
            if (string.IsNullOrEmpty(entry.Name)) continue;

            // Unix symlink girişlerini çıkarma (dışarıya işaret edebilir).
            if (IsSymlink(entry)) continue;

            if (entry.Length < 0 || entry.Length > MaxSingleEntry)
                throw new InvalidDataException(LocalizationService.T("Import_TooLarge"));

            var isManifest = string.Equals(entry.FullName.Replace('\\', '/'), "library.json",
                StringComparison.OrdinalIgnoreCase);
            if (isManifest && entry.Length > MaxLibraryJson)
                throw new InvalidDataException(LocalizationService.T("Import_TooLarge"));

            var destinationPath = ResolveInside(tempDir, entry.FullName);
            if (destinationPath == null) continue; // zip-slip

            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            ExtractEntry(entry, destinationPath, isManifest, ref total);
        }
    }

    /// <summary>
    /// Girişi gerçek yazılan baytları sayarak çıkarır. ZIP metadata'sındaki
    /// Length değeri kötü niyetli arşivlerde düşük gösterilebilir; bu yüzden
    /// limitler açılan gerçek veri üzerinden uygulanır.
    /// </summary>
    private static void ExtractEntry(ZipArchiveEntry entry, string destinationPath,
        bool isManifest, ref long total)
    {
        using var input = entry.Open();
        using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long entryBytes = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            entryBytes += read;
            total += read;
            if (entryBytes > MaxSingleEntry || total > MaxTotalUncompressed ||
                (isManifest && entryBytes > MaxLibraryJson))
                throw new InvalidDataException(LocalizationService.T("Import_TooLarge"));
            output.Write(buffer, 0, read);
        }
    }

    /// <summary>Göreli yolu güvenli biçimde kök dizin altına çözer; dışarı çıkıyorsa null.</summary>
    private static string? ResolveInside(string rootDir, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        if (Path.IsPathRooted(relativePath)) return null;

        var root = Path.GetFullPath(rootDir);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        string full;
        try
        {
            full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch
        {
            return null;
        }

        // StartsWith(root + separator) → "/tmp/x-evil" gibi kardeş dizinler geçemez.
        return full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    private static bool IsSymlink(ZipArchiveEntry entry)
    {
        // Unix modu üst 16 bitte; 0xA000 = symbolic link.
        var unixMode = (entry.ExternalAttributes >> 16) & 0xF000;
        return unixMode == 0xA000;
    }

    private static string SanitizeName(string? name)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0) return "";
        return trimmed.Length > 200 ? trimmed[..200] : trimmed;
    }

    private static string? SanitizeColor(string? color)
        => string.IsNullOrWhiteSpace(color) || color.Length > 32 ? null : color;

    private static string? SanitizeIcon(string? icon)
        => string.IsNullOrWhiteSpace(icon) ? null : (icon.Length > 8 ? icon[..8] : icon);
}
