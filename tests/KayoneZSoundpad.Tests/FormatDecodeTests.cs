using System.IO;
using Xunit;
using KayoneZSoundpad.Audio;

namespace KayoneZSoundpad.Tests;

/// <summary>
/// Gerçek ses dosyası format matrisi (ffmpeg ile üretilmiş varlıklar).
/// Varlık yoksa test sessizce geçer.
/// </summary>
public class FormatDecodeTests
{
    private static string AssetPath(string name)
    {
        var dir = AppContext.BaseDirectory;
        // TestAssets içerik olarak kopyalanır (csproj Content)
        return Path.Combine(dir, "TestAssets", name);
    }

    private static bool Exists(string name) => File.Exists(AssetPath(name));

    [Theory]
    [InlineData("test.mp3")]
    [InlineData("test.ogg")]
    [InlineData("test.flac")]
    public void Decode_RealFiles_To48kStereo(string asset)
    {
        if (!Exists(asset)) return; // varlık yoksa geç

        var cached = SoundDecoder.DecodeToCache(AssetPath(asset), 32 * 1024 * 1024);

        Assert.Equal(2, CachedSound.Format.Channels);
        Assert.Equal(48000, CachedSound.Format.SampleRate);
        // 1 sn'lik ses → en az ~0.9 sn örnek
        Assert.InRange(cached.DurationSeconds, 0.85, 1.15);

        // ses verisi sessiz değil (sinyal var)
        float peak = cached.Samples.Take(48000).Select(Math.Abs).Max();
        Assert.True(peak > 0.05f, $"{asset}: çözülen ses sinyalsiz görünüyor (peak={peak})");
    }

    [Theory]
    [InlineData("test.mp3")]
    [InlineData("test.ogg")]
    [InlineData("test.flac")]
    public void Duration_RealFiles_IsAboutOneSecond(string asset)
    {
        if (!Exists(asset)) return;
        var d = SoundDecoder.GetDuration(AssetPath(asset));
        Assert.InRange(d, 0.85, 1.2);
    }
}
