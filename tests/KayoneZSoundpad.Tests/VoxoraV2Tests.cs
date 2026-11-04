using NAudio.CoreAudioApi;
using NAudio.Wave;
using Xunit;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Services;

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
}
