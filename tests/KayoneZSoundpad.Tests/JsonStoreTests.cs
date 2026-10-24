using System.IO;
using Xunit;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Tests;

public class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "kztest-" + Guid.NewGuid().ToString("N")[..10]);

    public TempDir() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { }
    }
}

public class JsonStoreTests
{
    private class Sample
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public List<string>? Items { get; set; }
    }

    [Fact]
    public void Save_Then_Load_Roundtrips()
    {
        using var dir = new TempDir();
        var path = System.IO.Path.Combine(dir.Path, "data.json");
        var data = new Sample { Name = "test", Count = 7, Items = new List<string> { "a", "b" } };

        JsonStore.Save(path, data);
        var loaded = JsonStore.Load<Sample>(path);

        Assert.NotNull(loaded);
        Assert.Equal("test", loaded!.Name);
        Assert.Equal(7, loaded.Count);
        Assert.Equal(new[] { "a", "b" }, loaded.Items);
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        using var dir = new TempDir();
        var loaded = JsonStore.Load<Sample>(System.IO.Path.Combine(dir.Path, "yok.json"));
        Assert.Null(loaded);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsNull_AndBacksUp()
    {
        using var dir = new TempDir();
        var path = System.IO.Path.Combine(dir.Path, "data.json");
        File.WriteAllText(path, "{ bu bir bozuk json }");

        var loaded = JsonStore.Load<Sample>(path);

        Assert.Null(loaded);
        Assert.True(File.Exists(path + ".corrupt"));
    }

    [Fact]
    public void Save_OverwritesAtomically_NoTmpLeftover()
    {
        using var dir = new TempDir();
        var path = System.IO.Path.Combine(dir.Path, "data.json");
        JsonStore.Save(path, new Sample { Name = "1" });
        JsonStore.Save(path, new Sample { Name = "2" });

        var loaded = JsonStore.Load<Sample>(path);
        Assert.Equal("2", loaded!.Name);
        Assert.False(File.Exists(path + ".tmp"));
    }
}


