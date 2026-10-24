using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using KayoneZSoundpad.Models;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.ViewModels;

/// <summary>Kart üzerinde gösterilen tek ses efekti (UI sarmalayıcısı).</summary>
public class SoundCardViewModel : INotifyPropertyChanged
{
    public SoundItem Sound { get; }
    private readonly PlaybackManager _playback;

    public SoundCardViewModel(SoundItem sound, PlaybackManager playback)
    {
        Sound = sound;
        _playback = playback;
        playback.VoiceStopped += OnVoiceStopped;
    }

    private void OnVoiceStopped(Guid id)
    {
        if (id == Sound.Id)
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(IsPlaying));
            });
    }

    public Guid Id => Sound.Id;
    public string Name => Sound.Name;
    public string FilePath => Sound.FilePath;

    public string Icon => string.IsNullOrEmpty(Sound.Icon) ? "🎵" : Sound.Icon;

    /// <summary>Karta özel canlı ses seviyesi (0..150%). PlaybackManager üzerinden motorla senkronize kalır.</summary>
    public float Volume
    {
        get => Sound.Volume;
        set
        {
            var v = Math.Clamp(value, 0f, 1.5f);
            if (Math.Abs(Sound.Volume - v) < 0.001f) return;
            Sound.Volume = v;
            _playback.UpdateVoiceVolume(Id, v);
            OnPropertyChanged(nameof(Volume));
            OnPropertyChanged(nameof(VolumePercent));
        }
    }

    public int VolumePercent => (int)(Volume * 100);

    public bool IsMuted
    {
        get => Sound.IsMuted;
        set { Sound.IsMuted = value; _playback.UpdateVoiceVolume(Id, value ? 0f : Sound.Volume); OnPropertyChanged(nameof(IsMuted)); OnPropertyChanged(nameof(Volume)); }
    }

    public string DurationText
    {
        get
        {
            if (!Sound.FileExists) return "⚠ dosya yok";
            var d = Sound.DurationSeconds;
            if (d <= 0) return "";
            return TimeSpan.FromSeconds(d).ToString(d >= 60 ? @"m\:ss" : @"s\.f") + " sn";
        }
    }

    public string HotkeyText => Sound.Hotkey?.IsValid == true ? Sound.Hotkey.ToString() : "";

    public bool IsPlaying => _playback.IsPlaying(Sound.Id);

    public bool Favorite
    {
        get => Sound.Favorite;
        set { Sound.Favorite = value; OnPropertyChanged(nameof(Favorite)); OnPropertyChanged(nameof(StarGlyph)); }
    }

    public string StarGlyph => Sound.Favorite ? "★" : "☆";

    public System.Windows.Media.Brush AccentBrush
    {
        get
        {
            try
            {
                if (!string.IsNullOrEmpty(Sound.Color))
                    return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Sound.Color));
            }
            catch { }
            return (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["Brush.Accent"];
        }
    }

    public void RefreshAll()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(HotkeyText));
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(Favorite));
        OnPropertyChanged(nameof(StarGlyph));
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(VolumePercent));
        OnPropertyChanged(nameof(IsMuted));
        OnPropertyChanged(nameof(AccentBrush));
    }

    public void RefreshPlaying() => OnPropertyChanged(nameof(IsPlaying));

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}



