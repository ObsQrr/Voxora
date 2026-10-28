using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Models;
using NAudio.Wave;
using System.IO;

namespace KayoneZSoundpad.Services;

/// <summary>
/// Kütüphane ↔ ses motoru köprüsü: retrigger davranışı (yeniden başlat / üstüne çal / durdur),
/// basılı-tutma modu, önbellek önceliği + büyük dosyalarda streaming fallback.
/// </summary>
public class PlaybackManager
{
    private readonly AudioEngine _engine;
    private readonly SettingsService _settings;

    public PlaybackManager(AudioEngine engine, SettingsService settings)
    {
        _engine = engine;
        _settings = settings;
    }

    /// <summary>Voice durduğunda (doğal bitiş dahil) tetiklenir — UI IsPlaying göstergesini sıfırlar.</summary>
    public event Action<Guid>? VoiceStopped;

    /// <summary>Çalma başlatılamadığında hata mesajı.</summary>
    public event Action<Guid, string>? PlaybackError;

    public bool IsPlaying(Guid soundId) => _engine.IsPlaying(soundId);

    /// <summary>
    /// Kullanıcı sesi tetikledi (kart tıklaması veya kısayol basışı).
    /// holdMode: tuş hâlâ basılı (basılı tutma modu) → davranış uygulanmaz, çal.
    /// </summary>
    public void Trigger(SoundItem sound, bool holdMode = false)
    {
        var voice = _engine.GetVoice(sound.Id);
        if (voice != null && voice.IsPlaying && !holdMode)
        {
            switch (sound.Behavior)
            {
                case PlaybackBehavior.Toggle:
                    Stop(sound);
                    return;
                case PlaybackBehavior.Restart:
                    Stop(sound);
                    break;
                case PlaybackBehavior.Overlap:
                    break; // üstüne çal
            }
        }
        Start(sound, holdMode);
    }

    /// <summary>Basılı tutma modunda tuş bırakıldı.</summary>
    public void ReleaseHold(Guid soundId)
    {
        var voice = _engine.GetVoice(soundId);
        if (voice != null && voice.IsPlaying)
            _engine.StopVoice(voice);
    }

    public void Stop(SoundItem sound)
    {
        var voice = _engine.GetVoice(sound.Id);
        if (voice != null && voice.IsPlaying)
            _engine.StopVoice(voice);
    }

    public void StopAll() => _engine.StopAll();

    private void Start(SoundItem sound, bool holdMode)
    {
        if (!sound.FileExists)
        {
            PlaybackError?.Invoke(sound.Id, LocalizationService.T("Err_FileNotFound", sound.FilePath));
            return;
        }

        ISampleProvider source;
        var cached = _engine.TryCache(sound.Id, sound.FilePath, _settings.Settings.MaxCacheMb * 1024L * 1024L);
        if (cached != null)
        {
            source = cached.CreateProvider();
        }
        else
        {
            try
            {
                // OpenFile zaten 48 kHz / stereo float formatına dönüştürür.
                source = SoundDecoder.OpenFile(sound.FilePath);
            }
            catch (Exception ex)
            {
                PlaybackError?.Invoke(sound.Id, LocalizationService.T("Err_DecodeFailed", Path.GetFileName(sound.FilePath), ex.Message));
                return;
            }
        }

        // Sessiz (muted) sesler başlangıçta hiç ses çıkarmamalı.
        float volume = sound.IsMuted ? 0f : sound.Volume;
        var voice = _engine.Play(sound.Id, source, volume);
        if (voice == null)
        {
            PlaybackError?.Invoke(sound.Id, LocalizationService.T("Err_EngineNotRunning"));
            return;
        }

        if (holdMode)
            voice.HoldRequested = true;

        voice.Stopped += _ => VoiceStopped?.Invoke(sound.Id);
    }

    /// <summary>Ayarlar/durum değişince sesin seviyesini canlı güncelle.</summary>
    public void UpdateVoiceVolume(Guid soundId, float volume)
    {
        var voice = _engine.GetVoice(soundId);
        if (voice != null) voice.Volume.Volume = Math.Clamp(volume, 0f, 1.5f);
    }

    /// <summary>Dosyası değişen/silinen sesin bayat önbelleğini temizler.</summary>
    public void EvictCache(Guid soundId) => _engine.EvictCache(soundId);
}

