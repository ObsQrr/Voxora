using System.IO;
using KayoneZSoundpad.Models;

namespace KayoneZSoundpad.Services;

/// <summary>Ses kütüphanesi (kategoriler + sesler) kalıcılığı.</summary>
public class LibraryService
{
    private readonly string _path;
    private readonly object _saveLock = new();
    private readonly object _timerLock = new();
    private readonly bool _loadSucceeded;
    private Timer? _saveTimer;
    public LibraryData Data { get; private set; } = new();

    /// <summary>
    /// false ise mevcut library.json okunamadı (kilit/bozuk/çok büyük).
    /// Bu durumda Save() no-op olur; kullanıcının mevcut dosyası ezilmez.
    /// </summary>
    public bool LoadSucceeded => _loadSucceeded;

    public LibraryService(string? path = null)
    {
        _path = path ?? AppPaths.LibraryPath;
        var fileExists = File.Exists(_path);
        var loaded = JsonStore.Load<LibraryData>(_path);
        _loadSucceeded = loaded != null || !fileExists;
        Data = loaded ?? new LibraryData();
        Data.Categories ??= new List<Category>();
        Data.Sounds ??= new List<SoundItem>();
        // kopya olmayan kayıt durumunda sıralamayı garantiye al
        var next = Math.Max(Data.NextOrder, 1);
        foreach (var s in Data.Sounds) if (s.Order <= 0) s.Order = next++;
        foreach (var c in Data.Categories) if (c.Order <= 0) c.Order = next++;
        Data.NextOrder = next;
    }

    public void Save()
    {
        if (!_loadSucceeded) return; // yükleme başarısızsa mevcut dosyayı ezme
        lock (_saveLock) JsonStore.Save(_path, Data);
    }

    /// <summary>
    /// Sık değişen alanlar (ses seviyesi kaydırıcısı gibi) için 400 ms gecikmeli kayıt.
    /// Böylece her kaydırma adımında diske yazılmaz, ama değişiklik kalıcı olur.
    /// </summary>
    public void ScheduleSave()
    {
        lock (_timerLock)
        {
            _saveTimer ??= new Timer(_ =>
            {
                try { Save(); } catch { /* geçici yazma hatası sonraki kayıtta düzelir */ }
            }, null, Timeout.Infinite, Timeout.Infinite);
            _saveTimer.Change(400, Timeout.Infinite);
        }
    }

    public Category AddCategory(string name, string? color = null, string? icon = null)
    {
        var cat = new Category
        {
            Name = name.Trim(),
            Color = color,
            Icon = icon,
            Order = Data.NextOrder++
        };
        Data.Categories.Add(cat);
        Save();
        return cat;
    }

    public void RemoveCategory(Guid id)
    {
        var cat = Data.Categories.FirstOrDefault(c => c.Id == id);
        if (cat == null) return;
        Data.Categories.Remove(cat);
        foreach (var s in Data.Sounds.Where(s => s.CategoryId == id))
            s.CategoryId = null;
        Save();
    }

    public void RenameCategory(Guid id, string name)
    {
        var cat = Data.Categories.FirstOrDefault(c => c.Id == id);
        if (cat != null) { cat.Name = name.Trim(); Save(); }
    }

    /// <summary>Dosya ekler; gerekirse kütüphane dizinine kopyalar. Hata fırlatabilir.</summary>
    public SoundItem AddSound(string filePath, Guid? categoryId, bool copyToLibrary, string? color = null, string? icon = null, string? displayName = null)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException(LocalizationService.T("Err_SoundNotFound"), filePath);

        var target = filePath;
        if (copyToLibrary)
        {
            var dir = AppPaths.SoundsDir;
            Directory.CreateDirectory(dir);
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var ext = Path.GetExtension(filePath);
            var candidate = Path.Combine(dir, baseName + ext);
            int n = 1;
            while (File.Exists(candidate) && !PathsEqual(candidate, filePath))
            {
                candidate = Path.Combine(dir, $"{baseName} ({n++}){ext}");
            }
            if (!PathsEqual(candidate, filePath))
                File.Copy(filePath, candidate, overwrite: false);
            target = candidate;
        }

        var item = new SoundItem
        {
            Name = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileNameWithoutExtension(target) : displayName.Trim(),
            FilePath = target,
            CategoryId = categoryId,
            Color = color,
            Icon = icon,
            Behavior = PlaybackBehavior.Restart,
            Order = Data.NextOrder++
        };
        Data.Sounds.Add(item);
        Save();
        return item;
    }

    public void RemoveSound(Guid id)
    {
        var s = Data.Sounds.FirstOrDefault(x => x.Id == id);
        if (s != null) { Data.Sounds.Remove(s); Save(); }
    }

    private static bool PathsEqual(string a, string b)
        => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    public static bool IsSupportedFormat(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a" or ".aac" or ".wma" or ".opus";
    }
}
