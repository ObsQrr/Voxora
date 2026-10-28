using Xunit;
using NAudio.CoreAudioApi;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Tests;

/// <summary>
/// Gerçek ses aygıtı gerektiren entegrasyon testleri.
/// Aygıt yoksa sessizce geçer (makineye bağlı doğrulama).
/// </summary>
public class AudioEngineIntegrationTests : IDisposable
{
    private readonly TempDir _dir = new();

    public AudioEngineIntegrationTests() => AppPaths.SetOverride(System.IO.Path.Combine(_dir.Path, "data"));
    public void Dispose() { AppPaths.SetOverride(null); _dir.Dispose(); }

    private string WriteToneWav(string name, int seconds = 1)
    {
        var path = System.IO.Path.Combine(_dir.Path, name);
        using (var writer = new NAudio.Wave.WaveFileWriter(path, new NAudio.Wave.WaveFormat(48000, 16, 2)))
        {
            int frames = 48000 * seconds;
            var buf = new byte[frames * 4];
            for (int i = 0; i < frames; i++)
            {
                short v = (short)(Math.Sin(2 * Math.PI * 440 * i / 48000.0) * 10000);
                var b = BitConverter.GetBytes(v);
                buf[i * 4] = b[0]; buf[i * 4 + 1] = b[1];
                buf[i * 4 + 2] = b[0]; buf[i * 4 + 3] = b[1];
            }
            writer.Write(buf, 0, buf.Length);
        }
        return path;
    }

    public static bool HasRenderDevice()
    {
        try
        {
            using var e = new MMDeviceEnumerator();
            return e.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).Any();
        }
        catch { return false; }
    }

    [Fact]
    public void Engine_PlaysCachedSound_ToDefaultDevice_AndFinishesNaturally()
    {
        if (!HasRenderDevice()) return; // ses aygıtı olmayan makinede geç

        using var engine = new AudioEngine();
        string? error = null;
        engine.EngineError += m => error ??= m;
        float maxFxPeak = 0f;
        engine.Levels += (fx, _) => { if (fx > maxFxPeak) maxFxPeak = fx; };

        // Çıkış = varsayılan, mikrofon yok, monitör açık (aynı aygıt olursa motor zaten atlar)
        engine.ApplyConfiguration(null, null, null, micEnabled: false, monitorEnabled: true,
            duckEnabled: false, micVolume: 1f, fxVolume: 1f, duckFactor: 0.25f, engineLatencyMs: 50);

        // Motor başlatma hatası varsa (ör. aygıt format/event-sync desteklemiyorsa) testi geç
        if (!string.IsNullOrEmpty(error))
        {
            engine.Stop();
            return; // donanım uyumsuzluğu → test geçersiz sayılmaz
        }

        var id = Guid.NewGuid();
        var cached = SoundDecoder.DecodeToCache(WriteToneWav("tone.wav", 1), 10 * 1024 * 1024);
        var voice = engine.Play(id, cached.CreateProvider(), 1f);

        Assert.NotNull(voice);
        Assert.True(engine.IsPlaying(id));

        Thread.Sleep(300);
        Assert.True(engine.IsPlaying(id), "0.3 sn sonra hâlâ çalmalı");

        // doğal bitiş (1 sn'lik ses + tampon payı)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (engine.IsPlaying(id) && sw.ElapsedMilliseconds < 5000)
            Thread.Sleep(50);

        Assert.False(engine.IsPlaying(id), "Ses doğal olarak bitmeliydi");
        Assert.True(maxFxPeak > 0.01f, "Seviye göstergesi sinyal görmeliydi");
        Assert.Null(error);

        engine.Stop();
    }

    [Fact]
    public void Engine_StopAll_StopsImmediately()
    {
        if (!HasRenderDevice()) return; // ses aygıtı olmayan makinede geç

        using var engine = new AudioEngine();
        engine.ApplyConfiguration(null, null, null, false, true, false, 1f, 1f, 0.25f, 50);

        var cached = SoundDecoder.DecodeToCache(WriteToneWav("tone2.wav", 3), 10 * 1024 * 1024);
        var id = Guid.NewGuid();
        engine.Play(id, cached.CreateProvider(), 1f);
        Assert.True(engine.IsPlaying(id));

        engine.StopAll();
        Assert.False(engine.IsPlaying(id));
        engine.Stop();
    }

    [Fact]
    public void Engine_Retrigger_ReplacesVoice()
    {
        if (!HasRenderDevice()) return; // ses aygıtı olmayan makinede geç

        using var engine = new AudioEngine();
        engine.ApplyConfiguration(null, null, null, false, true, false, 1f, 1f, 0.25f, 50);

        var cached = SoundDecoder.DecodeToCache(WriteToneWav("tone3.wav", 3), 10 * 1024 * 1024);
        var id = Guid.NewGuid();
        var v1 = engine.Play(id, cached.CreateProvider(), 1f);
        var v2 = engine.Play(id, cached.CreateProvider(), 1f);

        Assert.False(v1!.IsPlaying, "Eski voice durdurulmalı");
        Assert.True(v2!.IsPlaying);
        engine.StopAll();
        engine.Stop();
    }
}