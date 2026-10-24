using System.IO;
using Xunit;
using KayoneZSoundpad.Models;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Tests;

public class ImportExportTests : IDisposable
{
    private readonly TempDir _dir = new();

    public ImportExportTests()
    {
        // kopyalama gerçek %APPDATA%'ya yazmasın
        AppPaths.SetOverride(System.IO.Path.Combine(_dir.Path, "data"));
    }

    public void Dispose() { AppPaths.SetOverride(null); _dir.Dispose(); }

    private string MakeWav(string name, int seconds = 1)
    {
        var path = System.IO.Path.Combine(_dir.Path, name);
        using var writer = new NAudio.Wave.WaveFileWriter(path, new NAudio.Wave.WaveFormat(48000, 16, 2));
        var samples = new byte[48000 * 4 * seconds];
        writer.Write(samples, 0, samples.Length);
        return path;
    }

    [Fact]
    public void Export_Then_Import_PreservesSounds()
    {
        var src = MakeWav("exp.wav");
        var libPath = System.IO.Path.Combine(_dir.Path, "library.json");
        var settingsPath = System.IO.Path.Combine(_dir.Path, "settings.json");

        var lib = new LibraryService(libPath);
        var settings = new SettingsService(settingsPath);
        var cat = lib.AddCategory("Komik", "#7C5CFF", "😂");
        var item = lib.AddSound(src, cat.Id, copyToLibrary: true, color: "#38BDF8", icon: "🔥", displayName: "Efekt1");
        item.Hotkey = new HotkeyGesture { VirtualKey = 0x70, Modifiers = HotkeyModifiers.Alt };
        item.Favorite = true;
        item.Volume = 0.8f;
        item.Behavior = PlaybackBehavior.Overlap;
        lib.Save();

        // dışa aktar (hedef kütüphaneye ayrı dizin)
        AppPaths.SetOverride(System.IO.Path.Combine(_dir.Path, "data2"));
        try
        {
            var importer = new ImportExportService(lib, settings);
            var zip = System.IO.Path.Combine(_dir.Path, "yedek.zip");
            int exported = importer.Export(zip);
            Assert.Equal(1, exported);
            Assert.True(File.Exists(zip));

            // yeni kütüphaneye içe aktar
            var lib2 = new LibraryService(System.IO.Path.Combine(_dir.Path, "library2.json"));
            var importer2 = new ImportExportService(lib2, settings);
            int imported = importer2.Import(zip, copyToLibrary: true);
            Assert.Equal(1, imported);

            var re = Assert.Single(lib2.Data.Sounds);
            Assert.Equal("Efekt1", re.Name);
            Assert.True(re.Favorite);
            Assert.Equal(0.8f, re.Volume, 3);
            Assert.Equal(PlaybackBehavior.Overlap, re.Behavior);
            Assert.NotNull(re.Hotkey);
            Assert.True(re.Hotkey!.Equals(new HotkeyGesture { VirtualKey = 0x70, Modifiers = HotkeyModifiers.Alt }));
            Assert.True(File.Exists(re.FilePath)); // kopyalandı
            var reCat = Assert.Single(lib2.Data.Categories);
            Assert.Equal("Komik", reCat.Name);
            Assert.Equal(reCat.Id, re.CategoryId);
        }
        finally
        {
            AppPaths.SetOverride(System.IO.Path.Combine(_dir.Path, "data"));
        }
    }

    [Fact]
    public void Import_InvalidZip_Throws()
    {
        var notZip = System.IO.Path.Combine(_dir.Path, "fake.zip");
        File.WriteAllText(notZip, "bu zip değil");
        var lib = new LibraryService(System.IO.Path.Combine(_dir.Path, "l.json"));
        var settings = new SettingsService(System.IO.Path.Combine(_dir.Path, "s.json"));
        var importer = new ImportExportService(lib, settings);
        Assert.ThrowsAny<Exception>(() => importer.Import(notZip, copyToLibrary: false));
    }
}


