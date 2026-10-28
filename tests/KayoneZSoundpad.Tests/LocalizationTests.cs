using Xunit;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Tests;

/// <summary>
/// Yerelleştirme sözlüklerinin bütünlüğü ve dil değiştirme davranışı.
/// Testler statik dil durumunu kirletmemek için her zaman eski dile döner.
/// </summary>
public class LocalizationTests
{
    [Fact]
    public void TurkishAndEnglish_HaveExactlyTheSameKeys()
    {
        var tr = LocalizationService.Strings("tr");
        var en = LocalizationService.Strings("en");

        var onlyTr = tr.Keys.Except(en.Keys).OrderBy(k => k).ToArray();
        var onlyEn = en.Keys.Except(tr.Keys).OrderBy(k => k).ToArray();

        Assert.True(onlyTr.Length == 0, "Sadece TR'de olan anahtarlar: " + string.Join(", ", onlyTr));
        Assert.True(onlyEn.Length == 0, "Sadece EN'de olan anahtarlar: " + string.Join(", ", onlyEn));
    }

    [Fact]
    public void EveryString_IsNonEmptyInBothLanguages()
    {
        foreach (var lang in new[] { "tr", "en" })
        {
            foreach (var kv in LocalizationService.Strings(lang))
            {
                Assert.False(string.IsNullOrWhiteSpace(kv.Value),
                    $"{lang}: '{kv.Key}' boş olamaz.");
            }
        }
    }

    [Fact]
    public void Normalize_FallsBackToTurkish()
    {
        Assert.Equal("en", LocalizationService.Normalize("EN"));
        Assert.Equal("tr", LocalizationService.Normalize(null));
        Assert.Equal("tr", LocalizationService.Normalize("de"));
        Assert.Equal("tr", LocalizationService.Normalize(""));
    }

    [Fact]
    public void T_FormatsArguments_AndFallsBackToKey()
    {
        var old = LocalizationService.Language;
        try
        {
            LocalizationService.SetLanguage("tr");
            var text = LocalizationService.T("Notify_ImportDone", 3);
            Assert.Contains("3", text);

            Assert.Equal("BuAnahtarYok", LocalizationService.T("BuAnahtarYok"));
            Assert.False(LocalizationService.HasKey("BuAnahtarYok", "tr"));
        }
        finally
        {
            LocalizationService.SetLanguage(old);
        }
    }

    [Fact]
    public void SetLanguage_SwitchesText_AndRaisesEventOnce()
    {
        var old = LocalizationService.Language;
        var raised = 0;
        Action handler = () => raised++;

        try
        {
            LocalizationService.SetLanguage("tr");
            LocalizationService.LanguageChanged += handler;

            var tr = LocalizationService.T("Common_Cancel");

            LocalizationService.SetLanguage("en");
            var en = LocalizationService.T("Common_Cancel");

            Assert.Equal("en", LocalizationService.Language);
            Assert.True(LocalizationService.IsEnglish);
            Assert.NotEqual(tr, en);
            Assert.Equal(1, raised);

            LocalizationService.Initialize("tr"); // notify=false → olay artmamalı
            Assert.Equal(1, raised);
            Assert.Equal("tr", LocalizationService.Language);
        }
        finally
        {
            LocalizationService.LanguageChanged -= handler;
            LocalizationService.SetLanguage(old);
        }
    }

    [Fact]
    public void ToStringNames_AreLocalized()
    {
        var old = LocalizationService.Language;
        try
        {
            LocalizationService.SetLanguage("tr");
            Assert.Equal("Sol Tuş", new KayoneZSoundpad.Models.HotkeyGesture
            {
                VirtualKey = 0x01
            }.ToString());

            LocalizationService.SetLanguage("en");
            Assert.Equal("Left Mouse", new KayoneZSoundpad.Models.HotkeyGesture
            {
                VirtualKey = 0x01
            }.ToString());
        }
        finally
        {
            LocalizationService.SetLanguage(old);
        }
    }
}
