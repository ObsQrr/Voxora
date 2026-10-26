using Xunit;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Models;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Tests;

/// <summary>
/// PlaybackManager davranış testleri: retrigger modları (yeniden başlat / üstüne çal /
/// durdur), basılı tutma, eksik dosya hatası — gerçek ses aygıtı üzerinden.
/// Aygıt yoksa sessizce geçer.
/// </summary>
public class PlaybackManagerTests : IDisposable
{
    private readonly TempDir _dir = new();

    public PlaybackManagerTests() => AppPaths.SetOverride(System.IO.Path.Combine(_dir.Path, "data"));
    public void Dispose() { AppPaths.SetOverride(null); _dir.Dispose(); }

    private static bool HasRenderDevice()
    {
        try
        {
            using var e = new MMDeviceEnumerator();
            return e.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).Any();
        }
        catch { return false; }
    }

    private static string WriteToneWav(TempDir dir, string name, int seconds)
    {
        var path = System.IO.Path.Combine(dir.Path, name);
        using (var writer = new WaveFileWriter(path, new WaveFormat(48000, 16, 2)))
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

    [Fact]
    public void Trigger_MissingFile_ReportsError_DoesNotPlay()
    {
        var settings = new SettingsService(System.IO.Path.Combine(_dir.Path, "s.json"));
        using var engine = new AudioEngine();
        var playback = new PlaybackManager(engine, settings);

        Guid? errorId = null;
        playback.PlaybackError += (id, _) => errorId ??= id;

        var sound = new SoundItem { Id = Guid.NewGuid(), Name = "yok", FilePath = System.IO.Path.Combine(_dir.Path, "yok.wav") };
        playback.Trigger(sound);

        Assert.Equal(sound.Id, errorId);
        Assert.False(playback.IsPlaying(sound.Id));
    }

    [Fact]
    public void Behavior_Toggle_StopsPlayingSound()
    {
        if (!HasRenderDevice()) return;

        var settings = new SettingsService(System.IO.Path.Combine(_dir.Path, "s.json"));
        using var engine = new AudioEngine();
        engine.ApplyConfiguration(null, null, null, false, true, false, 1f, 1f, 0.25f, 50);
        var playback = new PlaybackManager(engine, settings);

        var sound = new SoundItem
        {
            Id = Guid.NewGuid(),
            Name = "tone",
            FilePath = WriteToneWav(_dir, "toggle.wav", 3),
            Behavior = PlaybackBehavior.Toggle
        };

        playback.Trigger(sound);
        Assert.True(playback.IsPlaying(sound.Id));

        playback.Trigger(sound); // toggle → durdur
        Assert.False(playback.IsPlaying(sound.Id));
        engine.StopAll();
    }

    [Fact]
    public void Behavior_Restart_ReplaysFromBeginning()
    {
        if (!HasRenderDevice()) return;

        var settings = new SettingsService(System.IO.Path.Combine(_dir.Path, "s.json"));
        using var engine = new AudioEngine();
        engine.ApplyConfiguration(null, null, null, false, true, false, 1f, 1f, 0.25f, 50);
        var playback = new PlaybackManager(engine, settings);

        var sound = new SoundItem
        {
            Id = Guid.NewGuid(),
            Name = "tone",
            FilePath = WriteToneWav(_dir, "restart.wav", 3),
            Behavior = PlaybackBehavior.Restart
        };

        playback.Trigger(sound);
        Thread.Sleep(200);
        playback.Trigger(sound); // yeniden başlat → hâlâ çalıyor olmalı
        Assert.True(playback.IsPlaying(sound.Id));
        engine.StopAll();
    }

    [Fact]
    public void HoldMode_ReleaseHold_StopsSound()
    {
        if (!HasRenderDevice()) return;

        var settings = new SettingsService(System.IO.Path.Combine(_dir.Path, "s.json"));
        using var engine = new AudioEngine();
        engine.ApplyConfiguration(null, null, null, false, true, false, 1f, 1f, 0.25f, 50);
        var playback = new PlaybackManager(engine, settings);

        var sound = new SoundItem
        {
            Id = Guid.NewGuid(),
            Name = "tone",
            FilePath = WriteToneWav(_dir, "hold.wav", 3),
            HoldToPlay = true
        };

        playback.Trigger(sound, holdMode: true);
        Assert.True(playback.IsPlaying(sound.Id));

        playback.ReleaseHold(sound.Id); // tuş bırakıldı
        Assert.False(playback.IsPlaying(sound.Id));
        engine.StopAll();
    }

    [Fact]
    public void TwoSounds_PlaySimultaneously()
    {
        if (!HasRenderDevice()) return;

        var settings = new SettingsService(System.IO.Path.Combine(_dir.Path, "s.json"));
        using var engine = new AudioEngine();
        engine.ApplyConfiguration(null, null, null, false, true, false, 1f, 1f, 0.25f, 50);
        var playback = new PlaybackManager(engine, settings);

        var a = new SoundItem { Id = Guid.NewGuid(), Name = "a", FilePath = WriteToneWav(_dir, "sim_a.wav", 3) };
        var b = new SoundItem { Id = Guid.NewGuid(), Name = "b", FilePath = WriteToneWav(_dir, "sim_b.wav", 3) };

        playback.Trigger(a);
        playback.Trigger(b);

        Assert.True(playback.IsPlaying(a.Id));
        Assert.True(playback.IsPlaying(b.Id));
        Assert.Equal(2, engine.ActiveVoiceCount);

        playback.StopAll();
        Assert.False(playback.IsPlaying(a.Id));
        Assert.False(playback.IsPlaying(b.Id));
    }

    [Fact]
    public void UpdateVoiceVolume_ChangesLiveVolume()
    {
        if (!HasRenderDevice()) return;

        var settings = new SettingsService(System.IO.Path.Combine(_dir.Path, "s.json"));
        using var engine = new AudioEngine();
        engine.ApplyConfiguration(null, null, null, false, true, false, 1f, 1f, 0.25f, 50);
        var playback = new PlaybackManager(engine, settings);

        var sound = new SoundItem { Id = Guid.NewGuid(), Name = "tone", FilePath = WriteToneWav(_dir, "vol.wav", 3) };
        playback.Trigger(sound);

        var voice = engine.GetVoice(sound.Id);
        Assert.NotNull(voice);
        Assert.Equal(1f, voice!.Volume.Volume, 3);

        playback.UpdateVoiceVolume(sound.Id, 0.5f);
        Assert.Equal(0.5f, voice.Volume.Volume, 3);

        playback.StopAll();
    }
}
