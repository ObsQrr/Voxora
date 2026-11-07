using NAudio.CoreAudioApi;
using NAudio.Wave;
using Xunit;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Features;
using KayoneZSoundpad.Models;
using KayoneZSoundpad.Services;
using KayoneZSoundpad.Views;

namespace KayoneZSoundpad.Tests;

/// <summary>2.0 arayüz/ilk kurulum ve Windows cihaz adı davranışları.</summary>
public class VoxoraV2Tests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Theory]
    [InlineData("CABLE Output (VB-Audio Virtual Cable)")]
    [InlineData("CABLE Input (VB-Audio Virtual Cable)")]
    [InlineData("Voxora Out Audio")]
    [InlineData("Voxora In Audio")]
    public void IsCableName_RecognizesVirtualCables(string name)
        => Assert.True(DeviceService.IsCableName(name));

    [Theory]
    [InlineData("Speakers (Realtek(R) Audio)")]
    [InlineData("Microphone (USB Audio)")]
    [InlineData("Kulaklıklar (HyperX Cloud III)")]
    public void IsCableName_RejectsRealDevices(string name)
        => Assert.False(DeviceService.IsCableName(name));

    [Fact]
    public void GetDisplayName_KeepsRenamedEndpointsStable()
    {
        Assert.Equal("Voxora In Audio",
            DeviceService.GetDisplayName(DataFlow.Render, "Voxora In Audio"));
        Assert.Equal("Voxora Out Audio",
            DeviceService.GetDisplayName(DataFlow.Capture, "Voxora Out Audio"));
    }

    [Theory]
    [InlineData("Voxora Out Audio", true)]
    [InlineData("Voxora In Audio (VB-Audio Virtual Cable)", true)]
    [InlineData("CABLE Output", false)]
    public void IsVoxoraName_DetectsOurBrand(string name, bool expected)
        => Assert.Equal(expected, AudioEndpointNamingService.IsVoxoraName(name));

    [Fact]
    public void ToneSampleProvider_IsBoundedAndFinishes()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
        var tone = new ToneSampleProvider(format, 660, 0.1);
        var buffer = new float[4800 * 2];

        int read = tone.Read(buffer, 0, buffer.Length);

        Assert.Equal(buffer.Length, read);
        Assert.All(buffer.Take(read), value =>
        {
            Assert.False(float.IsNaN(value));
            Assert.InRange(value, -0.23f, 0.23f);
        });

        var tail = new float[480];
        Assert.Equal(0, tone.Read(tail, 0, tail.Length));
    }

    [Fact]
    public void OnboardingCompleted_RoundTripsThroughSettings()
    {
        var path = System.IO.Path.Combine(_dir.Path, "settings.json");
        var service = new SettingsService(path);
        Assert.False(service.Settings.OnboardingCompleted);

        service.Settings.OnboardingCompleted = true;
        service.Save();

        Assert.True(new SettingsService(path).Settings.OnboardingCompleted);
    }

    [Fact]
    public void SettingsWithoutOnboarding_DefaultsToNotCompleted()
    {
        var path = System.IO.Path.Combine(_dir.Path, "settings.json");
        System.IO.File.WriteAllText(path, "{\"theme\":\"dark\",\"language\":\"tr\"}");

        Assert.False(new SettingsService(path).Settings.OnboardingCompleted);
    }

    [Fact]
    public void DefaultTheme_IsDarkForNewProfiles()
    {
        Assert.Equal("dark", new KayoneZSoundpad.Models.AppSettings().Theme);
    }

    [Fact]
    public void DefaultRadialHotkey_IsF1()
    {
        var settings = new AppSettings();
        Assert.NotNull(settings.RadialMenuHotkey);
        Assert.Equal(0x70, settings.RadialMenuHotkey!.VirtualKey);
        Assert.Equal(HotkeyModifiers.None, settings.RadialMenuHotkey.Modifiers);
    }

    [Fact]
    public void RadialMenu_IsEnabledByDefault()
    {
        var definition = FeatureCatalog.Get(FeatureId.RadialMenu);
        Assert.True(definition.DefaultEnabled);
        Assert.Equal(FeatureAvailability.Available, definition.Availability);
    }

    [Fact]
    public void RadialHotkey_RoundTripsThroughSettings()
    {
        var path = System.IO.Path.Combine(_dir.Path, "radial.json");
        var service = new SettingsService(path);
        service.Settings.RadialMenuHotkey = new HotkeyGesture
        {
            VirtualKey = 0x71,
            Modifiers = HotkeyModifiers.Ctrl
        };
        service.Save();

        var reloaded = new SettingsService(path).Settings.RadialMenuHotkey!;
        Assert.Equal(0x71, reloaded.VirtualKey);
        Assert.Equal(HotkeyModifiers.Ctrl, reloaded.Modifiers);
        Assert.Equal("Ctrl + F2", reloaded.ToString());
    }

    [Fact]
    public void RadialPaging_SplitsIntoPagesOfEight()
    {
        var items = Enumerable.Range(0, 18).ToArray();

        Assert.Equal(3, RadialPaging.PageCount(items.Length));
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, RadialPaging.PageItems(items, 0));
        Assert.Equal(new[] { 8, 9, 10, 11, 12, 13, 14, 15 }, RadialPaging.PageItems(items, 1));
        Assert.Equal(new[] { 16, 17 }, RadialPaging.PageItems(items, 2));
    }

    [Fact]
    public void RadialPaging_ExactlyEight_IsSinglePage()
    {
        var items = Enumerable.Range(0, 8).ToArray();
        Assert.Equal(1, RadialPaging.PageCount(items.Length));
        Assert.Equal(items, RadialPaging.PageItems(items, 0));
        Assert.Equal(0, RadialPaging.Next(0, 1, items.Length));
    }

    [Fact]
    public void SettingsWithoutRadialHotkey_GetsF1Default()
    {
        var path = System.IO.Path.Combine(_dir.Path, "settings-no-radial.json");
        System.IO.File.WriteAllText(path, "{\"theme\":\"dark\",\"language\":\"tr\"}");

        var reloaded = new SettingsService(path).Settings.RadialMenuHotkey!;
        Assert.Equal(0x70, reloaded.VirtualKey);
        Assert.Equal(HotkeyModifiers.None, reloaded.Modifiers);
    }

    [Fact]
    public void SettingsWithClearedRadialHotkey_StaysCleared()
    {
        var path = System.IO.Path.Combine(_dir.Path, "settings-null-radial.json");
        System.IO.File.WriteAllText(path,
            "{\"theme\":\"dark\",\"language\":\"tr\",\"radialMenuHotkey\":null}");

        Assert.Null(new SettingsService(path).Settings.RadialMenuHotkey);
    }

    [Fact]
    public void RadialPaging_WrapsBothDirections()
    {
        Assert.Equal(2, RadialPaging.Next(0, -1, 18));
        Assert.Equal(2, RadialPaging.Next(1, 1, 18));
        Assert.Equal(0, RadialPaging.Next(2, 1, 18));
        Assert.Equal(0, RadialPaging.Next(0, 1, 3));
        Assert.Equal(0, RadialPaging.Next(0, -1, 0));
    }
}
