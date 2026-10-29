using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Hotkeys;
using KayoneZSoundpad.Models;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.ViewModels;

/// <summary>Kenar çubuğundaki kategori seçenekleri (Tümü / Favoriler / gerçek kategoriler).</summary>
public class CategoryEntry
{
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "📁";
    public Guid? Id { get; init; }  // null=Tümü, Guid.Empty=Favoriler, gerçek id
    public int Count { get; set; }

    public bool IsAll => Id is null;
    public bool IsFavorites => Id == Guid.Empty;

    public override string ToString() => Name;
}

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly LibraryService _library;
    private readonly AudioEngine _engine;
    private readonly PlaybackManager _playback;
    private readonly GlobalHotkeyService _hotkeys;
    private readonly DeviceService _devices;
    private readonly ImportExportService _importExport;
    private readonly Action<float, float> _onLevels;
    private readonly Action<string> _onEngineError;
    private readonly Action<string> _onEngineInfo;
    private readonly Action _onDevicesChanged;
    private bool _disposed;
    public static readonly Guid StopAllActionId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public ObservableCollection<SoundCardViewModel> Sounds { get; } = new();
    public ObservableCollection<CategoryEntry> Categories { get; } = new();
    public ObservableCollection<DeviceEntry> RenderDevices { get; } = new();
    public ObservableCollection<DeviceEntry> CaptureDevices { get; } = new();

    private CategoryEntry? _selectedCategory;
    public CategoryEntry? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            OnPropertyChanged(nameof(SelectedCategory));
            ApplyFilter();
            SaveLastCategory();
        }
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(nameof(SearchText)); ApplyFilter(); }
    }

    private float _fxPeak, _outPeak;
    public float FxPeak { get => _fxPeak; private set { _fxPeak = value; OnPropertyChanged(nameof(FxPeak)); } }
    public float OutPeak { get => _outPeak; private set { _outPeak = value; OnPropertyChanged(nameof(OutPeak)); } }

    private string _cableStatusText = "";
    public string CableStatusText { get => _cableStatusText; private set { _cableStatusText = value; OnPropertyChanged(nameof(CableStatusText)); } }

    public AppSettings Settings => _settingsService.Settings;
    public LibraryService Library => _library;
    public PlaybackManager Playback => _playback;
    public GlobalHotkeyService Hotkeys => _hotkeys;
    public DeviceService Devices => _devices;
    public SettingsService SettingsService => _settingsService;

    public void SaveSettings() => _settingsService.Save();

    // mixer barı canlı kontroller (kaydetmeyi UI sürükleme bitişinde SaveSettings ile yapar)
    public void SetMicVolume(float v) { Settings.MicVolume = Math.Clamp(v, 0f, 1.5f); ApplyEngineConfig(); }
    public void SetFxVolume(float v) { Settings.FxVolume = Math.Clamp(v, 0f, 1.5f); ApplyEngineConfig(); }
    public void SetMicEnabled(bool on) { Settings.MicEnabled = on; ApplyEngineConfig(); _settingsService.Save(); }
    public void SetMonitorEnabled(bool on) { Settings.MonitorEnabled = on; ApplyEngineConfig(); _settingsService.Save(); }

    public event Action<SoundItem>? EditSoundRequested;
    public event Action? SettingsRequested;
    public event Action? CableGuideRequested;
    public event Action<string>? NotifyInfo;
    public event Action<string>? NotifyError;

    public MainViewModel(SettingsService settingsService, LibraryService library,
        AudioEngine engine, PlaybackManager playback, GlobalHotkeyService hotkeys, DeviceService devices)
    {
        _settingsService = settingsService;
        _library = library;
        _engine = engine;
        _playback = playback;
        _hotkeys = hotkeys;
        _devices = devices;
        _importExport = new ImportExportService(library, settingsService);
        _selectedCategory = new CategoryEntry { Name = LocalizationService.T("Category_All"), Icon = "🎧", Id = null };

        _onLevels = (fx, outp) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_disposed) return;
            FxPeak = fx;
            OutPeak = outp;
        });
        _onEngineError = msg => System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (!_disposed) NotifyError?.Invoke(msg);
        });
        _onEngineInfo = msg => System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (!_disposed) NotifyInfo?.Invoke(msg);
        });
        _onDevicesChanged = () => System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_disposed) return;
            RefreshDevices();
            NotifyInfo?.Invoke(LocalizationService.T("Notify_DevicesChanged"));
        });

        _engine.Levels += _onLevels;
        _engine.EngineError += _onEngineError;
        _engine.EngineInfo += _onEngineInfo;
        _devices.DevicesChanged += _onDevicesChanged;

        _hotkeys.HotkeyPressed += OnHotkeyPressed;
        _hotkeys.HotkeyReleased += OnHotkeyReleased;
        LocalizationService.LanguageChanged += OnLanguageChanged;

        LoadSounds();
        RefreshDevices();
        // VB-CABLE kurulduysa ve Çıkış Voxora In Audio değilse otomatik kur (bozuk/eksik ayar self-heal)
        try
        {
            var cableIn = _devices.FindCableRender();
            bool outputIsCable = Settings.OutputDeviceId != null && RenderDevices.Any(d => d.Id == Settings.OutputDeviceId && d.IsCable);
            if (cableIn != null && !outputIsCable) AutoConfigureCable();
        }
        catch { }
    }

    // ------------------------------------------------------------ yükleme/filtre

    private void LoadSounds()
    {
        foreach (var old in Sounds) old.Dispose();
        Sounds.Clear();
        foreach (var s in _library.Data.Sounds.OrderBy(s => s.Order))
        {
            var card = new SoundCardViewModel(s, _playback, _library.ScheduleSave);
            Sounds.Add(card);
        }
        RebuildCategories();
        // son kategoriyi hatırla
        if (Settings.RememberLastCategory && Settings.LastCategoryId != null)
        {
            var entry = Categories.FirstOrDefault(c => c.Id == Settings.LastCategoryId);
            if (entry != null) { _selectedCategory = entry; OnPropertyChanged(nameof(SelectedCategory)); }
        }
        ApplyFilter();
    }

    public void AddSoundToUI(SoundItem sound)
    {
        Sounds.Add(new SoundCardViewModel(sound, _playback, _library.ScheduleSave));
        RebuildCategories();
        ApplyFilter();
    }

    public void RemoveSoundFromUI(Guid id)
    {
        var card = Sounds.FirstOrDefault(s => s.Id == id);
        if (card != null)
        {
            _playback.Stop(card.Sound);
            _playback.EvictCache(id);
            Sounds.Remove(card);
            card.Dispose();
        }
        RebuildCategories();
        ApplyFilter();
    }

    public void RebuildCategories()
    {
        var selId = _selectedCategory?.Id;
        Categories.Clear();
        Categories.Add(new CategoryEntry { Name = LocalizationService.T("Category_All"), Icon = "🎧", Id = null });
        Categories.Add(new CategoryEntry { Name = LocalizationService.T("Category_Favorites"), Icon = "⭐", Id = Guid.Empty });
        foreach (var c in _library.Data.Categories.OrderBy(c => c.Order))
            Categories.Add(new CategoryEntry { Name = c.Name, Icon = string.IsNullOrEmpty(c.Icon) ? "📁" : c.Icon, Id = c.Id });

        foreach (var e in Categories)
        {
            e.Count = e.IsAll ? Sounds.Count
                : e.IsFavorites ? Sounds.Count(s => s.Favorite)
                : Sounds.Count(s => s.Sound.CategoryId == e.Id);
        }
        // seçili kategori silinmiş olabilir
        var again = Categories.FirstOrDefault(c => Equals(c.Id, selId));
        _selectedCategory = again ?? Categories[0];
        OnPropertyChanged(nameof(SelectedCategory));
    }

    private void ApplyFilter()
    {
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(Sounds);
        string q = SearchText?.Trim() ?? "";
        var sel = SelectedCategory;
        view.Filter = o =>
        {
            if (o is not SoundCardViewModel c) return false;
            if (!string.IsNullOrEmpty(q) &&
                c.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) return false;

            if (sel == null || sel.IsAll) return true;
            if (sel.IsFavorites) return c.Favorite;
            return c.Sound.CategoryId == sel.Id;
        };
        FilterApplied?.Invoke();
        SaveLastCategory();
    }

    /// <summary>Kütüphane dosyadan yeniden yüklendiğinde UI'ı tazeler.</summary>
    public void ReloadSounds() => LoadSounds();

    public event Action? FilterApplied;

    private void SaveLastCategory()
    {
        if (!Settings.RememberLastCategory) return;
        Settings.LastCategoryId = SelectedCategory?.Id;
    }

    public void RefreshCard(Guid id)
    {
        var card = Sounds.FirstOrDefault(s => s.Id == id);
        card?.RefreshAll();
        RebuildCategories();
        ApplyFilter();
    }

    public SoundCardViewModel? GetCard(Guid id) => Sounds.FirstOrDefault(s => s.Id == id);

    private void OnLanguageChanged()
    {
        RebuildCategories();
        RefreshDevices();
        foreach (var c in Sounds) c.RefreshAll();
        OnPropertyChanged(nameof(CableStatusText));
    }

    // ------------------------------------------------------------ kısayollar

    private void OnHotkeyPressed(Guid actionId)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (actionId == StopAllActionId)
            {
                _playback.StopAll();
                foreach (var c in Sounds) c.RefreshPlaying();
                return;
            }
            var sound = _library.Data.Sounds.FirstOrDefault(s => s.Id == actionId);
            if (sound == null) return;
            if (sound.HoldToPlay)
                _playback.Trigger(sound, holdMode: true);
            else
                TriggerFromUI(sound);
        });
    }

    private void OnHotkeyReleased(Guid actionId)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            var sound = _library.Data.Sounds.FirstOrDefault(s => s.Id == actionId);
            if (sound != null && sound.HoldToPlay)
            {
                _playback.ReleaseHold(sound.Id);
                GetCard(sound.Id)?.RefreshPlaying();
            }
        });
    }

    public void TriggerFromUI(SoundItem sound)
    {
        _playback.Trigger(sound);
        GetCard(sound.Id)?.RefreshPlaying();
    }

    /// <summary>Tüm kısayolları (sesler + stop-all) yeniden kaydeder. Çakışma uyarısı döner.</summary>
    public string RegisterAllHotkeys()
    {
        _hotkeys.Clear();
        var conflicts = new List<string>();
        var seen = new Dictionary<HotkeyGesture, string>();

        if (Settings.StopAllHotkey?.IsValid == true)
        {
            _hotkeys.Register(Settings.StopAllHotkey, StopAllActionId);
            seen[Settings.StopAllHotkey] = LocalizationService.T("Hotkey_StopAllOwner");
        }

        foreach (var s in _library.Data.Sounds)
        {
            if (s.Hotkey?.IsValid != true) continue;
            if (seen.TryGetValue(s.Hotkey, out var owner))
                conflicts.Add(LocalizationService.T("Hotkey_ConflictBoth", s.Hotkey, owner, s.Name));
            else
                seen[s.Hotkey] = s.Name;
            _hotkeys.Register(s.Hotkey, s.Id);
        }

        _hotkeys.SuppressMatching = Settings.SuppressHotkeys;
        _hotkeys.Start();
        return string.Join("\n", conflicts);
    }

    /// <summary>Yeni kısayol atarken çakışma kontrolü. Sorun yoksa null.</summary>
    public string? CheckConflict(HotkeyGesture gesture, Guid ownerSoundId)
    {
        if (gesture?.IsValid != true) return null;
        if (Settings.StopAllHotkey?.Equals(gesture) == true)
            return LocalizationService.T("Hotkey_AssignedToStopAll", gesture);
        var other = _library.Data.Sounds.FirstOrDefault(s => s.Id != ownerSoundId && s.Hotkey?.Equals(gesture) == true);
        return other != null ? LocalizationService.T("Hotkey_AssignedToSound", gesture, other.Name) : null;
    }

    // ------------------------------------------------------------ aygıtlar

    public void RefreshDevices()
    {
        var renders = _devices.GetRenderDevices();
        var captures = _devices.GetCaptureDevices();

        RenderDevices.Clear();
        foreach (var d in renders) RenderDevices.Add(d);
        CaptureDevices.Clear();
        foreach (var d in captures) CaptureDevices.Add(d);

        OnPropertyChanged(nameof(RenderDevices));
        OnPropertyChanged(nameof(CaptureDevices));

        // Voxora sanal aygıtı durumu
        var cableIn = renders.FirstOrDefault(d => d.IsCable);
        var cableOut = captures.FirstOrDefault(d => d.IsCable);
        bool routingActive = Settings.OutputDeviceId != null &&
                             renders.Any(d => d.Id == Settings.OutputDeviceId && d.IsCable);

        CableStatusText = (cableIn != null && cableOut != null)
            ? (routingActive ? LocalizationService.T("Cable_Active") : LocalizationService.T("Cable_InstalledNotRouted"))
            : LocalizationService.T("Cable_Missing");

        ApplyEngineConfig();
    }

    public void ApplyEngineConfig()
    {
        _engine.ApplyConfiguration(
            Settings.OutputDeviceId,
            Settings.MicDeviceId,
            Settings.MonitorDeviceId,
            Settings.MicEnabled,
            Settings.MonitorEnabled,
            Settings.DuckMicWhilePlaying,
            Settings.MicVolume,
            Settings.FxVolume,
            Settings.DuckFactor,
            Settings.EngineLatencyMs);
    }

    // Not: ComboBox'ta SelectedValue iki yönlü bağlı olduğu için Settings.* zaten
    // seçim anında değişmiş olur. Bu yüzden "değer aynıysa çık" erken dönüşü
    // kullanılmaz; aksi halde kaydetme ve motoru uygulama adımı atlanıyordu.
    public void SetOutputDevice(DeviceEntry? d)
    {
        Settings.OutputDeviceId = d?.Id;
        _settingsService.Save();
        ApplyEngineConfig();
        OnPropertyChanged(nameof(CableStatusText));
        OnPropertyChanged(nameof(IsCableRoutingActive));
    }

    public void SetMicDevice(DeviceEntry? d)
    {
        Settings.MicDeviceId = d?.Id;
        _settingsService.Save();
        ApplyEngineConfig();
    }

    public void SetMonitorDevice(DeviceEntry? d)
    {
        Settings.MonitorDeviceId = d?.Id;
        _settingsService.Save();
        ApplyEngineConfig();
    }

    /// <summary>
    /// Voxora sanal aygıtları varsa tam mikrofon yönlendirmesini otomatik kurar:
    ///   Çıkış  = Voxora In Audio   → Discord/oyun burayı mikrofon olarak alır
    ///   Mikrofon = gerçek mikrofon (Voxora Out Audio değil — geri besleme olmaz)
    ///   Monitör = Voxora olmayan varsayılan çıkış (lokal dinleme)
    /// </summary>
    public bool AutoConfigureCable()
    {
        var cableIn = _devices.FindCableRender();
        var cableOut = _devices.FindCableCapture();
        if (cableIn == null || cableOut == null) return false;

        // 1) Ana çıkış = Voxora In Audio
        Settings.OutputDeviceId = cableIn.Id;

        // 2) Mikrofon kaynağı = Voxora olmayan gerçek mikrofon (mevcut geçerli ayar korunur)
        var currentMic = CaptureDevices.FirstOrDefault(d => d.Id == Settings.MicDeviceId);
        if (currentMic == null || currentMic.IsCable)
        {
            var realMic = CaptureDevices.FirstOrDefault(d => !d.IsCable && !d.OriginalName.Contains("Steam Streaming"))
                          ?? CaptureDevices.FirstOrDefault(d => !d.IsCable);
            if (realMic != null) Settings.MicDeviceId = realMic.Id;
        }

        // 3) Monitör = Voxora olmayan çıkış (varsayılan tercih edilir)
        var currentMon = RenderDevices.FirstOrDefault(d => d.Id == Settings.MonitorDeviceId);
        if (currentMon == null || currentMon.IsCable)
        {
            var realMon = RenderDevices.FirstOrDefault(d => !d.IsCable && d.IsDefault)
                          ?? RenderDevices.FirstOrDefault(d => !d.IsCable);
            if (realMon != null) Settings.MonitorDeviceId = realMon.Id;
        }

        _settingsService.Save();
        ApplyEngineConfig();
        RefreshDevices();
        return true;
    }

    /// <summary>Yönlendirme yapılandırılmış mı? (çıkış Voxora In Audio ise hazır)</summary>
    public bool IsCableRoutingActive =>
        RenderDevices.Any(d => d.Id == Settings.OutputDeviceId && d.IsCable);

    public DeviceEntry? GetSelectedRender() =>
        RenderDevices.FirstOrDefault(d => d.Id == Settings.OutputDeviceId)
        ?? RenderDevices.FirstOrDefault(d => d.IsDefault)
        ?? RenderDevices.FirstOrDefault();

    public DeviceEntry? GetSelectedCapture() =>
        CaptureDevices.FirstOrDefault(d => d.Id == Settings.MicDeviceId)
        ?? CaptureDevices.FirstOrDefault(d => d.IsDefault)
        ?? CaptureDevices.FirstOrDefault();

    public DeviceEntry? GetSelectedMonitor() =>
        RenderDevices.FirstOrDefault(d => d.Id == Settings.MonitorDeviceId)
        ?? RenderDevices.FirstOrDefault(d => d.IsDefault)
        ?? RenderDevices.FirstOrDefault();

    // ------------------------------------------------------------ komutlar

    public RelayCommand AddFilesCommand => new(p =>
    {
        if (p is string[] files && files.Length > 0) AddFiles(files);
        else AddFilesRequested?.Invoke();
    });
    public RelayCommand AddFolderCommand => new(p =>
    {
        if (p is string folder && !string.IsNullOrWhiteSpace(folder)) AddFolder(folder);
        else AddFolderRequested?.Invoke();
    });
    public RelayCommand ImportLibraryCommand => new(path => ImportLibrary(path as string));
    public RelayCommand ExportLibraryCommand => new(path => ExportLibrary(path as string));
    public RelayCommand StopAllCommand => new(_ =>
    {
        _playback.StopAll();
        foreach (var c in Sounds) c.RefreshPlaying();
    });
    public RelayCommand PlayStopCommand => new(p =>
    {
        if (p is SoundCardViewModel c) TriggerFromUI(c.Sound);
    });
    public RelayCommand ToggleFavoriteCommand => new(p =>
    {
        if (p is SoundCardViewModel c)
        {
            c.Favorite = !c.Favorite;
            _library.Save();
            RebuildCategories();
            ApplyFilter();
        }
    });
    public RelayCommand EditSoundCommand => new(p =>
    {
        if (p is SoundCardViewModel c) EditSoundRequested?.Invoke(c.Sound);
    });
    public RelayCommand DeleteSoundCommand => new(p =>
    {
        if (p is SoundCardViewModel c)
        {
            _library.RemoveSound(c.Id);
            RemoveSoundFromUI(c.Id);
            RegisterAllHotkeys();
        }
    });
    public RelayCommand OpenSettingsCommand => new(_ => SettingsRequested?.Invoke());
    public RelayCommand OpenCableGuideCommand => new(_ => CableGuideRequested?.Invoke());
    public RelayCommand AddCategoryCommand => new(_ => AddCategoryRequested?.Invoke());
    public RelayCommand ToggleMuteCommand => new(p =>
    {
        if (p is SoundCardViewModel c)
        {
            c.IsMuted = !c.IsMuted;
            _library.Save();
        }
    });
    public RelayCommand DeleteCategoryCommand => new(p =>
    {
        if (p is CategoryEntry e && e.Id is Guid id && id != Guid.Empty)
        {
            _library.RemoveCategory(id);
            RebuildCategories();
            SelectedCategory = Categories[0];
        }
    });

    /// <summary>Yeni kategori oluşturur (dialog'dan çağrılır).</summary>
    public void AddCategory(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        _library.AddCategory(name.Trim());
        RebuildCategories();
    }

    /// <summary>
    /// Yeni sesin atanacağı kategori. "Tüm Sesler" ve "Favoriler" gerçek bir
    /// kategori değildir; ikisinde de yeni ses kategorisiz eklenir.
    /// </summary>
    private Guid? TargetCategoryId =>
        SelectedCategory is { IsAll: false, IsFavorites: false } ? SelectedCategory.Id : null;

    public void AddFiles(string[] paths)
    {
        foreach (var p in paths)
        {
            if (!File.Exists(p)) continue;
            if (!LibraryService.IsSupportedFormat(p)) continue;
            try { _library.AddSound(p, TargetCategoryId, Settings.CopyOnImport); }
            catch (Exception ex) { NotifyError?.Invoke(LocalizationService.T("Notify_AddFailed", ex.Message)); }
        }
        ReloadSounds();
        RegisterAllHotkeys();
    }

    public void AddFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(f => LibraryService.IsSupportedFormat(f));
        foreach (var f in files)
        {
            try { _library.AddSound(f, TargetCategoryId, Settings.CopyOnImport); }
            catch (Exception ex) { NotifyError?.Invoke(LocalizationService.T("Notify_AddFailed", ex.Message)); }
        }
        ReloadSounds();
        RegisterAllHotkeys();
    }

    public void ImportLibrary(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            int added = _importExport.Import(path, copyToLibrary: true);
            ReloadSounds();
            RegisterAllHotkeys();
            NotifyInfo?.Invoke(LocalizationService.T("Notify_ImportDone", added));
        }
        catch (Exception ex)
        {
            NotifyError?.Invoke(LocalizationService.T("Notify_ImportFailed", ex.Message));
        }
    }

    public void ExportLibrary(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            int exported = _importExport.Export(path);
            NotifyInfo?.Invoke(LocalizationService.T("Notify_ExportDone", exported, path));
        }
        catch (Exception ex)
        {
            NotifyError?.Invoke(LocalizationService.T("Notify_ExportFailed", ex.Message));
        }
    }

    /// <summary>Tüm kişisel ses dosyalarını (meta veri olmadan) ZIP olarak dışa aktarır.</summary>
    public void ExportSoundFiles(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            int exported = _importExport.ExportSoundFiles(path);
            if (exported == 0)
                NotifyInfo?.Invoke(LocalizationService.T("Notify_ExportSoundsEmpty"));
            else
                NotifyInfo?.Invoke(LocalizationService.T("Notify_ExportSoundsDone", exported, path));
        }
        catch (Exception ex)
        {
            NotifyError?.Invoke(LocalizationService.T("Notify_ExportSoundsFailed", ex.Message));
        }
    }

    public event Action? AddFilesRequested;
    public event Action? AddFolderRequested;
    public event Action? AddCategoryRequested;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        _engine.Levels -= _onLevels;
        _engine.EngineError -= _onEngineError;
        _engine.EngineInfo -= _onEngineInfo;
        _devices.DevicesChanged -= _onDevicesChanged;
        _hotkeys.HotkeyPressed -= OnHotkeyPressed;
        _hotkeys.HotkeyReleased -= OnHotkeyReleased;
    }
}
