using System.ComponentModel;
using KayoneZSoundpad.Models;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.ViewModels;

/// <summary>Kart üzerinde gösterilen tek ses efekti (UI sarmalayıcısı).</summary>
public class SoundCardViewModel : INotifyPropertyChanged, IDisposable
{
    public SoundItem Sound { get; }
    private readonly PlaybackManager _playback;
    private readonly Action? _saveLibrary;
    private bool _disposed;

    public SoundCardViewModel(SoundItem sound, PlaybackManager playback, Action? saveLibrary = null)
    {
        Sound = sound;
        _playback = playback;
        _saveLibrary = saveLibrary;
        playback.VoiceStopped += OnVoiceStopped;
    }

    private void OnVoiceStopped(Guid id)
    {
        if (_disposed || id != Sound.Id) return;
        // UI iş parçacığına bloklamadan dön (Invoke yerine BeginInvoke).
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (!_disposed) OnPropertyChanged(nameof(IsPlaying));
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
            _playback.UpdateVoiceVolume(Id, Sound.IsMuted ? 0f : v);
            OnPropertyChanged(nameof(Volume));
            OnPropertyChanged(nameof(VolumePercent));
            _saveLibrary?.Invoke();
        }
    }

    public int VolumePercent => (int)(Volume * 100);

    public bool IsMuted
    {
        get => Sound.IsMuted;
        set
        {
            if (Sound.IsMuted == value) return;
            Sound.IsMuted = value;
            _playback.UpdateVoiceVolume(Id, value ? 0f : Sound.Volume);
            OnPropertyChanged(nameof(IsMuted));
            OnPropertyChanged(nameof(Volume));
            _saveLibrary?.Invoke();
        }
    }

    public string DurationText
    {
        get
        {
            if (!Sound.FileExists) return LocalizationService.T("Card_FileMissing");
            var d = Sound.DurationSeconds;
            if (d <= 0) return "";
            var text = TimeSpan.FromSeconds(d).ToString(d >= 60 ? @"m\:ss" : @"s\.f");
            return text + " " + LocalizationService.T("Common_SecondsShort");
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
                    return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Sound.Color));
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _playback.VoiceStopped -= OnVoiceStopped;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
