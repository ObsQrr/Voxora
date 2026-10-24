using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KayoneZSoundpad.Services;

/// <summary>JSON dosya kalıcılığı: atomik yazma (tmp + replace), bozuk dosyada null döner.</summary>
public static class JsonStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        // Elle düzenlenmiş/başka araçla üretilmiş dosyalar da okunsun (id / Id / ID fark etmez)
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static T? Load<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var fs = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(fs, Options);
        }
        catch (Exception)
        {
            // Bozuk/erişilemeyen dosya uygulamanın çökmesine neden olmasın;
            // .corrupt yedeğine taşı.
            try
            {
                if (File.Exists(path))
                    File.Copy(path, path + ".corrupt", overwrite: true);
            }
            catch { /* yedekleme başarısız olsa da devam */ }
            return null;
        }
    }

    public static void Save<T>(string path, T data)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        using (var fs = File.Create(tmp))
        {
            JsonSerializer.Serialize(fs, data, Options);
            fs.Flush();
        }
        File.Move(tmp, path, overwrite: true);
    }
}
