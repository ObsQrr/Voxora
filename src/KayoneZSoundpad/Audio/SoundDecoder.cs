using NAudio.Wave.SampleProviders;
using System.IO;
using NAudio.Wave;

namespace KayoneZSoundpad.Audio;

/// <summary>
/// Çözülmüş ses: 48 kHz / 2 kanal / IEEE float, kesişimli (interleaved) örnekler.
/// Bellekte tutulur → kısayolla çalarken decode gecikmesi yaşanmaz.
/// </summary>
public class CachedSound
{
    public const int SampleRate = 48000;
    public const int Channels = 2;
    public static readonly WaveFormat Format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);

    public float[] Samples { get; }
    public double DurationSeconds { get; }

    public CachedSound(float[] samples, double durationSeconds)
    {
        Samples = samples;
        DurationSeconds = durationSeconds;
    }

    public CachedSoundSampleProvider CreateProvider() => new(this);
}

public class CachedSoundSampleProvider : ISampleProvider
{
    private readonly CachedSound _sound;
    private int _position;

    public CachedSoundSampleProvider(CachedSound sound) => _sound = sound;

    public WaveFormat WaveFormat => CachedSound.Format;

    public int Read(float[] buffer, int offset, int count)
    {
        int available = _sound.Samples.Length - _position;
        if (available <= 0) return 0;
        int toCopy = Math.Min(count, available);
        Array.Copy(_sound.Samples, _position, buffer, offset, toCopy);
        _position += toCopy;
        return toCopy;
    }
}

/// <summary>Herhangi bir ses dosyasını 48 kHz stereo float'a çevirir.</summary>
public static class SoundDecoder
{
    /// <summary>Desteklenen uzantılar.</summary>
    public static readonly string[] SupportedExtensions =
        { ".mp3", ".wav", ".ogg", ".flac", ".m4a", ".aac", ".wma", ".opus" };

    public static bool IsSupported(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return SupportedExtensions.Contains(ext);
    }

    /// <summary>Format uygun NAudio okuyucusunu açar. Desteklenmeyen/bozuk dosyada hata fırlatır.</summary>
    public static ISampleProvider OpenFile(string path)
    {
        var reader = CreateReader(path);
        try
        {
            var formatted = ToEngineFormat(reader.ToSampleProvider());
            // Alt akış (dosya tutamacı) çalma bitince serbest bırakılabilmeli.
            return new OwnedSampleProvider(formatted, reader);
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    private static WaveStream CreateReader(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".wav" => new WaveFileReader(path),
            ".mp3" => new Mp3FileReader(path),
            ".ogg" => new NAudio.Vorbis.VorbisWaveReader(path),
            _ => new MediaFoundationReader(path)
        };
    }

    /// <summary>Kaynağı 48 kHz / 2 kanal float'a yeniden örnekler.</summary>
    public static ISampleProvider ToEngineFormat(ISampleProvider source)
    {
        var sp = source;
        if (sp.WaveFormat.SampleRate != CachedSound.SampleRate)
            sp = new WdlResamplingSampleProvider(sp, CachedSound.SampleRate);
        if (sp.WaveFormat.Channels == 1)
            sp = new MonoToStereoSampleProvider(sp);
        else if (sp.WaveFormat.Channels > 2)
            sp = new FoldToStereoSampleProvider(sp);
        return sp;
    }

    /// <summary>Dosya süresi (saniye). Hata olursa 0.</summary>
    public static double GetDuration(string path)
    {
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            using WaveStream ws = ext switch
            {
                ".wav" => new WaveFileReader(path),
                ".mp3" => new Mp3FileReader(path),
                ".ogg" => new NAudio.Vorbis.VorbisWaveReader(path),
                _ => new MediaFoundationReader(path)
            };
            return ws.TotalTime.TotalSeconds;
        }
        catch { return 0; }
    }

    /// <summary>
    /// Dosyayı tamamen belleğe çözerek CachedSound üretir. Hata fırlatır.
    /// maxBytes aşıldığında CacheTooLargeException fırlatır (çağıran streaming'e düşer).
    /// </summary>
    public static CachedSound DecodeToCache(string path, long maxBytes)
    {
        var reader = OpenFile(path);
        try
        {
            var chunks = new List<float[]>();
            long totalFloats = 0;
            var buf = new float[48000 * 2 * 4]; // ~1 sn
            int read;
            while ((read = reader.Read(buf, 0, buf.Length)) > 0)
            {
                var copy = new float[read];
                Array.Copy(buf, copy, read);
                chunks.Add(copy);
                totalFloats += read;
                if (totalFloats * sizeof(float) > maxBytes)
                    throw new CacheTooLargeException();
            }
            var samples = new float[totalFloats];
            int pos = 0;
            foreach (var c in chunks) { Array.Copy(c, 0, samples, pos, c.Length); pos += c.Length; }
            double duration = totalFloats / (double)(CachedSound.Channels * CachedSound.SampleRate);
            return new CachedSound(samples, duration);
        }
        finally
        {
            (reader as IDisposable)?.Dispose();
        }
    }
}

/// <summary>
/// Bir örnek sağlayıcıyı sarar ve altındaki dosya/akış kaynağını serbest bırakır.
/// PlaybackManager, akış (streaming) çalma bitince veya durdurulunca bunu dispose eder.
/// </summary>
public class OwnedSampleProvider : ISampleProvider, IDisposable
{
    private readonly ISampleProvider _inner;
    private readonly IDisposable _owner;
    private bool _disposed;

    public OwnedSampleProvider(ISampleProvider inner, IDisposable owner)
    {
        _inner = inner;
        _owner = owner;
    }

    public WaveFormat WaveFormat => _inner.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
        => _disposed ? 0 : _inner.Read(buffer, offset, count);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { (_inner as IDisposable)?.Dispose(); } catch { }
        try { _owner.Dispose(); } catch { }
    }
}

public class CacheTooLargeException : Exception
{
    public CacheTooLargeException() : base("Dosya bellek önbelleği için çok büyük; akış modunda çalınacak.") { }
}

/// <summary>Tek kanallı (mono) örnek sağlayıcıyı stereo'ya çoğaltır.</summary>
public class MonoToStereoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    public MonoToStereoSampleProvider(ISampleProvider source)
    {
        if (source.WaveFormat.Channels != 1) throw new ArgumentException("Kaynak mono olmalı.");
        _source = source;
    }

    public WaveFormat WaveFormat => CachedSound.Format;

    public int Read(float[] buffer, int offset, int count)
    {
        int frames = count / 2;
        // mono girişi buffer'ın sonuna oku (çakışmayı önlemek için)
        if (_monoBuf.Length < frames) _monoBuf = new float[frames];
        int read = _source.Read(_monoBuf, 0, frames);
        for (int i = 0; i < read; i++)
        {
            buffer[offset + i * 2] = _monoBuf[i];
            buffer[offset + i * 2 + 1] = _monoBuf[i];
        }
        return read * 2;
    }

    private float[] _monoBuf = Array.Empty<float>();
}

/// <summary>Çok kanallı (3+) kaynağı stereo'ya katlar.</summary>
public class FoldToStereoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private float[] _srcBuf = Array.Empty<float>();

    public FoldToStereoSampleProvider(ISampleProvider source)
    {
        if (source.WaveFormat.Channels < 3) throw new ArgumentException("Kaynak en az 3 kanal olmalı.");
        _source = source;
    }

    public WaveFormat WaveFormat => CachedSound.Format;

    public int Read(float[] buffer, int offset, int count)
    {
        int srcCh = _source.WaveFormat.Channels;
        int frames = count / 2;
        if (_srcBuf.Length < frames * srcCh) _srcBuf = new float[frames * srcCh];
        int read = _source.Read(_srcBuf, 0, frames * srcCh);
        int inFrames = read / srcCh;
        for (int f = 0; f < inFrames; f++)
        {
            float l = 0, r = 0;
            for (int c = 0; c < srcCh; c += 2) l += _srcBuf[f * srcCh + c];
            for (int c = 1; c < srcCh; c += 2) r += _srcBuf[f * srcCh + c];
            buffer[offset + f * 2] = l;
            buffer[offset + f * 2 + 1] = r;
        }
        return inFrames * 2;
    }
}


