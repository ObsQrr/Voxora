using System.IO;
using System.Xml.Linq;
using Xunit;

namespace KayoneZSoundpad.Tests;

public class ThemeResourceTests
{
    private static HashSet<string> Keys(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestAssets", fileName);
        Assert.True(File.Exists(path), $"Test asset missing: {path}");

        var doc = XDocument.Load(path);
        return doc.Descendants()
            .SelectMany(e => e.Attributes())
            .Where(a => a.Name.LocalName == "Key")
            .Select(a => a.Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void LightAndDarkThemesDefineSameResourceKeys()
    {
        var dark = Keys("Theme.Dark.xaml");
        var light = Keys("Theme.Light.xaml");

        var missingInLight = dark.Except(light).OrderBy(k => k).ToArray();
        var missingInDark = light.Except(dark).OrderBy(k => k).ToArray();

        Assert.True(missingInLight.Length == 0,
            "Theme.Light is missing: " + string.Join(", ", missingInLight));
        Assert.True(missingInDark.Length == 0,
            "Theme.Dark is missing: " + string.Join(", ", missingInDark));
    }

    [Fact]
    public void BothThemesProvideIconStyles()
    {
        foreach (var file in new[] { "Theme.Dark.xaml", "Theme.Light.xaml" })
        {
            var keys = Keys(file);
            Assert.Contains("Icon", keys);
            Assert.Contains("IconSm", keys);
        }
    }
}
