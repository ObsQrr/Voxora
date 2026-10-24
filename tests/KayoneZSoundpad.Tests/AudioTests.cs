using System.IO;
using Xunit;
using NAudio.Wave;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Tests;

public class AudioTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private string WriteWav(string name, int sampleRate = 48000, int channels = 2, int seconds = 1)
    {
        var path = System.IO.Path.Combine(_dir.Path, name);
        using (var writer = new WaveFileWriter(path, new WaveFormat(sampleRate, 16, channels)))
        {
            int frameBytes = channels * 2;
            var buf = new byte[sampleRate * frameBytes * seconds];
            for (int i = 0; i < buf.Length / frameBytes; i++)
            {
                short v = (short)(Math.Sin(2 * Math.PI * 440 * i / (double)sampleRate) * 12000);
                for (int c = 0; c < channels; c++)
                {
                    var b = BitConverter.GetBytes(v);
                    buf[(i * frameBytes) + c * 2] = b[0];
                    buf[(i * frameBytes) + c * 2 + 1] = b[1];
                }
            }
            writer.Write(buf, 0, buf.Length);
        }
        return path;
    }

    [Fact]
    public void Decoder_Wav_Cache_OutputFormat_Is48kStereo()
    {
        var path = WriteWav("test.wav", 44100, 1);
        var cached = SoundDecoder.DecodeToCache(path, 64 * 1024 * 1024);

        Assert.Equal(48000, CachedSound.Format.SampleRate);
        Assert.Equal(2, CachedSound.Format.Channels);
        Assert.True(cached.Samples.Length > 48000); // en az 1 sn stereo
        Assert.InRange(cached.DurationSeconds, 0.9, 1.1);

        // tüm örnekler sınırlar içinde
        Assert.All(cached.Samples.Take(10000), s => Assert.InRange(s, -1f, 1f));
    }

    [Fact]
    public void Decoder_Duration_MatchesFile()
    {
        var path = WriteWav("dur.wav", 48000, 2, 2);
        var duration = SoundDecoder.GetDuration(path);
        Assert.InRange(duration, 1.9, 2.1);
    }

    [Fact]
    public void Decoder_CorruptFile_Throws()
    {
        var path = System.IO.Path.Combine(_dir.Path, "broken.wav");
        File.WriteAllText(path, "BU SES DOSYASI DEĞİL");
        Assert.ThrowsAny<Exception>(() => SoundDecoder.OpenFile(path));
    }

    [Fact]
    public void Decoder_CacheLimit_ThrowsTooLarge()
    {
        var path = WriteWav("big.wav", 48000, 2, 3);
        Assert.Throws<CacheTooLargeException>(() => SoundDecoder.DecodeToCache(path, 16 * 1024));
    }

    [Fact]
    public void CachedSoundProvider_ReadsSequentially()
    {
        var samples = new float[] { 1, 2, 3, 4, 5, 6 };
        var sound = new CachedSound(samples, 0.0000625);
        var provider = sound.CreateProvider();

        var buf = new float[4];
        Assert.Equal(4, provider.Read(buf, 0, 4));
        Assert.Equal(new float[] { 1, 2, 3, 4 }, buf[..4]);
        Assert.Equal(2, provider.Read(buf, 0, 4));
        Assert.Equal(new float[] { 5, 6 }, buf[..2]); // yalnız yazılan kısım geçerli
        Assert.Equal(0, provider.Read(buf, 0, 4));
    }

    [Fact]
    public void MonoToStereo_Duplicates()
    {
        var mono = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var src = new StaticSampleProvider(mono, WaveFormat.CreateIeeeFloatWaveFormat(48000, 1));
        var stereo = new MonoToStereoSampleProvider(src);

        var buf = new float[8];
        int read = stereo.Read(buf, 0, 8);
        Assert.Equal(8, read);
        Assert.Equal(new float[] { 0.1f, 0.1f, 0.2f, 0.2f, 0.3f, 0.3f, 0.4f, 0.4f }, buf);
    }

    [Fact]
    public void Limit_KeepsSignal_InBounds()
    {
        Assert.Equal(0.5f, AudioEngine.Limit(0.5f), 3);
        Assert.InRange(AudioEngine.Limit(2.0f), -1f, 1f);
        Assert.InRange(AudioEngine.Limit(-2.0f), -1f, 1f);
        Assert.InRange(AudioEngine.Limit(0.999f), -1f, 1f);
        // limiter çıkışı asla 1'i aşamaz
        var rnd = new Random(7);
        for (int i = 0; i < 10000; i++)
        {
            float x = (float)(rnd.NextDouble() * 4 - 2);
            Assert.InRange(AudioEngine.Limit(x), -1.001f, 1.001f);
        }
    }

    [Fact]
    public void Engine_TryCache_CachesAndEvicts()
    {
        var path = WriteWav("cache.wav", 48000, 2, 1);
        using var engine = new AudioEngine();
        var id = Guid.NewGuid();

        var cached = engine.TryCache(id, path, 32 * 1024 * 1024);
        Assert.NotNull(cached);
        Assert.True(engine.CacheBytes > 0);

        engine.EvictCache(id);
        Assert.Equal(0, engine.CacheBytes);
    }

    [Fact]
    public void IsSupported_ChecksExtensions()
    {
        Assert.True(SoundDecoder.IsSupported("x.MP3"));
        Assert.True(SoundDecoder.IsSupported("x.ogg"));
        Assert.False(SoundDecoder.IsSupported("x.png"));
    }

    private class StaticSampleProvider : ISampleProvider
    {
        private readonly float[] _data;
        private int _pos;
        public WaveFormat WaveFormat { get; }
        public StaticSampleProvider(float[] data, WaveFormat format) { _data = data; WaveFormat = format; }
        public int Read(float[] buffer, int offset, int count)
        {
            int n = Math.Min(count, _data.Length - _pos);
            Array.Copy(_data, _pos, buffer, offset, n);
            _pos += n;
            return n;
        }
    }
}


