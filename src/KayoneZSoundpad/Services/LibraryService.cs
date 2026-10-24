using System.IO;
using KayoneZSoundpad.Models;

namespace KayoneZSoundpad.Services;

/// <summary>Ses kütüphanesi (kategoriler + sesler) kalıcılığı.</summary>
public class LibraryService
{
    private readonly string _path;
    private readonly object _saveLock = new();
    public LibraryData Data { get; private set; } = new();

    public LibraryService(string? path = null)
    {
        _path = path ?? AppPaths.LibraryPath;
        Data = JsonStore.Load<LibraryData>(_path) ?? new LibraryData();
        // kopya olmayan kayıt durumunda sıralamayı garantiye al
        var next = Math.Max(Data.NextOrder, 1);
        foreach (var s in Data.Sounds) if (s.Order <= 0) s.Order = next++;
        foreach (var c in Data.Categories) if (c.Order <= 0) c.Order = next++;
        Data.NextOrder = next;
    }

    public void Save()
    {
        lock (_saveLock) JsonStore.Save(_path, Data);
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
        if (!File.Exists(filePath)) throw new FileNotFoundException("Ses dosyası bulunamadı.", filePath);

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
