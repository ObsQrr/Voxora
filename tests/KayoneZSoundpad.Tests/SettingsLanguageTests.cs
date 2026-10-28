using System.IO;
using Xunit;
using KayoneZSoundpad.Models;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Tests;

public class SettingsLanguageTests : IDisposable
{
    private readonly TempDir _dir = new();

    public SettingsLanguageTests() => AppPaths.SetOverride(Path.Combine(_dir.Path, "data"));
    public void Dispose() { AppPaths.SetOverride(null); _dir.Dispose(); }

    [Fact]
    public void DefaultLanguage_IsTurkish()
    {
        var svc = new SettingsService(Path.Combine(_dir.Path, "settings.json"));
        Assert.Equal("tr", svc.Settings.Language);
        Assert.Equal("light", svc.Settings.Theme);
    }

    [Fact]
    public void Language_IsPersisted_AndReloaded()
    {
        var path = Path.Combine(_dir.Path, "settings.json");
        var svc = new SettingsService(path);
        svc.Settings.Language = "en";
        svc.Save();

        var reloaded = new SettingsService(path);
        Assert.Equal("en", reloaded.Settings.Language);
    }

    [Fact]
    public void InvalidLanguage_IsNormalizedToTurkish()
    {
        var path = Path.Combine(_dir.Path, "settings.json");
        var invalid = new AppSettings { Language = "de", Theme = "neon" };
        JsonStore.Save(path, invalid);

        var svc = new SettingsService(path);
        Assert.Equal("tr", svc.Settings.Language);
        Assert.Equal("light", svc.Settings.Theme);
    }

    [Fact]
    public void UnknownJsonFields_AndBrokenFile_DoNotCrash()
    {
        var path = Path.Combine(_dir.Path, "settings.json");
        File.WriteAllText(path, "{ \"theme\": \"dark\", \"language\": \"en\", \"future\": 123 }");

        var svc = new SettingsService(path);
        Assert.Equal("en", svc.Settings.Language);
        Assert.Equal("dark", svc.Settings.Theme);

        File.WriteAllText(path, "{ bozuk json");
        var broken = new SettingsService(path);
        Assert.Equal("tr", broken.Settings.Language);
        Assert.True(File.Exists(path + ".corrupt"));
    }
}
