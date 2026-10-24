using System.IO;
using Xunit;
using KayoneZSoundpad.Models;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Tests;

public class LibraryServiceTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly string _libPath;

    public LibraryServiceTests()
    {
        _libPath = System.IO.Path.Combine(_dir.Path, "library.json");
        AppPaths.SetOverride(System.IO.Path.Combine(_dir.Path, "data"));
    }

    public void Dispose() { AppPaths.SetOverride(null); _dir.Dispose(); }

    private string MakeWav(string name, int seconds = 1)
    {
        var path = System.IO.Path.Combine(_dir.Path, name);
        using var writer = new NAudio.Wave.WaveFileWriter(path,
            new NAudio.Wave.WaveFormat(48000, 16, 2));
        var samples = new byte[48000 * 4]; // 1 sn stereo 16-bit
        new Random(42).NextBytes(samples);
        writer.Write(samples, 0, samples.Length);
        return path;
    }

    [Fact]
    public void AddSound_CopiesFile_AndCreatesEntry()
    {
        var src = MakeWav("test1.wav");
        var lib = new LibraryService(_libPath);

        var item = lib.AddSound(src, null, copyToLibrary: true);

        Assert.True(File.Exists(item.FilePath));
        Assert.Contains("Sounds", item.FilePath);
        Assert.False(string.Equals(src, item.FilePath, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("test1", item.Name);
        Assert.Single(lib.Data.Sounds);
    }

    [Fact]
    public void AddSound_SameNameTwice_GetsUniqueFile()
    {
        var src = MakeWav("dup.wav");
        var lib = new LibraryService(_libPath);

        var a = lib.AddSound(src, null, true);
        var b = lib.AddSound(src, null, true);

        Assert.NotEqual(a.FilePath, b.FilePath);
        Assert.True(File.Exists(b.FilePath));
    }

    [Fact]
    public void AddSound_MissingFile_Throws()
    {
        var lib = new LibraryService(_libPath);
        Assert.Throws<FileNotFoundException>(() =>
            lib.AddSound(System.IO.Path.Combine(_dir.Path, "yok.wav"), null, false));
    }

    [Fact]
    public void Category_Add_Remove_ReassignsSounds()
    {
        var lib = new LibraryService(_libPath);
        var src = MakeWav("cat.wav");
        var cat = lib.AddCategory("Komik");
        var item = lib.AddSound(src, cat.Id, false);

        lib.RemoveCategory(cat.Id);

        Assert.Empty(lib.Data.Categories);
        Assert.Null(lib.Data.Sounds[0].CategoryId);
        Assert.Single(lib.Data.Sounds);
    }

    [Fact]
    public void RemoveSound_RemovesEntry()
    {
        var lib = new LibraryService(_libPath);
        var src = MakeWav("del.wav");
        var item = lib.AddSound(src, null, false);

        lib.RemoveSound(item.Id);

        Assert.Empty(lib.Data.Sounds);
    }

    [Fact]
    public void Persistence_Reloads_Sounds_And_Hotkeys()
    {
        var src = MakeWav("persist.wav");
        var lib = new LibraryService(_libPath);
        var cat = lib.AddCategory("Oyun", "#7C5CFF", "🎮");
        var item = lib.AddSound(src, cat.Id, false, color: "#FF5C7A", icon: "😂", displayName: "Selam");
        item.Hotkey = new HotkeyGesture { VirtualKey = 0x70, Modifiers = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt };
        item.Favorite = true;
        lib.Save();

        var lib2 = new LibraryService(_libPath);

        var reloaded = Assert.Single(lib2.Data.Sounds);
        Assert.Equal("Selam", reloaded.Name);
        Assert.Equal(cat.Id, reloaded.CategoryId);
        Assert.True(reloaded.Favorite);
        Assert.NotNull(reloaded.Hotkey);
        Assert.True(reloaded.Hotkey!.Equals(new HotkeyGesture { VirtualKey = 0x70, Modifiers = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt }));
        var reloadedCat = Assert.Single(lib2.Data.Categories);
        Assert.Equal("Oyun", reloadedCat.Name);
    }

    [Fact]
    public void IsSupportedFormat_ChecksExtensions()
    {
        Assert.True(LibraryService.IsSupportedFormat("a.mp3"));
        Assert.True(LibraryService.IsSupportedFormat("a.FLAC"));
        Assert.False(LibraryService.IsSupportedFormat("a.exe"));
        Assert.False(LibraryService.IsSupportedFormat("a.txt"));
    }
}


