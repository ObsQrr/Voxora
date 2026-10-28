using NAudio.CoreAudioApi;
using Xunit;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Tests;

/// <summary>
/// VB-CABLE eşleştirmesi daima Windows'un gerçek adı üzerinden yapılmalı;
/// kullanıcıya gösterilen Voxora markalı ad teknik karşılaştırmada kullanılmamalı.
/// </summary>
public class DeviceNamingTests
{
    [Theory]
    [InlineData(DataFlow.Render, "CABLE Input (VB-Audio Virtual Cable)", "Voxora In Audio")]
    [InlineData(DataFlow.Capture, "CABLE Output (VB-Audio Virtual Cable)", "Voxora Out Audio")]
    [InlineData(DataFlow.Render, "Speakers (Realtek(R) Audio)", "Speakers (Realtek(R) Audio)")]
    [InlineData(DataFlow.Capture, "Microphone (USB Audio)", "Microphone (USB Audio)")]
    public void GetDisplayName_BrandsOnlyRealCableEndpoints(DataFlow flow, string original, string expected)
    {
        Assert.Equal(expected, DeviceService.GetDisplayName(flow, original));
    }

    [Fact]
    public void DisplayName_DefaultSuffix_IsLocalized()
    {
        var old = LocalizationService.Language;
        try
        {
            var entry = new DeviceEntry { Name = "Hoparlör", IsDefault = true };

            LocalizationService.SetLanguage("tr");
            Assert.Contains("varsayılan", entry.DisplayName);

            LocalizationService.SetLanguage("en");
            Assert.Contains("default", entry.DisplayName);
            Assert.DoesNotContain("varsayılan", entry.DisplayName);
        }
        finally
        {
            LocalizationService.SetLanguage(old);
        }
    }
}
