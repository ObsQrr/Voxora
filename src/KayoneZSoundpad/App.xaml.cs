using System.IO;
using System.Threading;
using System.Windows;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Hotkeys;
using KayoneZSoundpad.Services;
using KayoneZSoundpad.ViewModels;
using KayoneZSoundpad.Views;

namespace KayoneZSoundpad;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstanceMutex;

    public static AppPathsHolder? Paths { get; private set; }
    public MainViewModel? ViewModel { get; private set; }

    private MainWindow? _mainWindow;

    public class AppPathsHolder
    {
        public SettingsService SettingsService { get; init; } = null!;
        public LibraryService LibraryService { get; init; } = null!;
        public AudioEngine Engine { get; init; } = null!;
        public PlaybackManager Playback { get; init; } = null!;
        public GlobalHotkeyService Hotkeys { get; init; } = null!;
        public DeviceService Devices { get; init; } = null!;
        public UpdateService UpdateService { get; init; } = null!;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Dil, herhangi bir metin üretilmeden önce yüklenmeli (varsayılan: Türkçe).
        LocalizationService.Initialize(LocalizationService.Turkish);

        // tek örnek
        _singleInstanceMutex = new Mutex(true, "KayoneZSoundpad_SingleInstance", out var isNew);
        if (!isNew)
        {
            System.Windows.MessageBox.Show(LocalizationService.T("App_AlreadyRunning"),
                "Voxora", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // yakalanmayan hata -> günlüğe yaz, uygulama kapanmasın
        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "voxora-ui.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + " DISPATCHER EXCEPTION: " + args.Exception.ToString() + Environment.NewLine + Environment.NewLine);
            }
            catch { }
            System.Windows.MessageBox.Show(
                LocalizationService.T("App_UnexpectedError", args.Exception.Message, args.Exception.StackTrace),
                LocalizationService.T("App_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppPaths.EnsureDirs();

        var settingsService = new SettingsService();

        // Kaydedilmiş dil tercihini uygula (varsayılan Türkçe korunur).
        LocalizationService.Initialize(settingsService.Settings.Language);

        var libraryService = new LibraryService();
        if (!libraryService.LoadSucceeded)
        {
            System.Windows.MessageBox.Show(
                LocalizationService.T("Library_LoadFailed", AppPaths.LibraryPath),
                LocalizationService.T("App_ErrorTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        var engine = new AudioEngine();
        var playback = new PlaybackManager(engine, settingsService);
        var hotkeys = new GlobalHotkeyService();
        var devices = new DeviceService();
        var updates = new UpdateService();

        Paths = new AppPathsHolder
        {
            SettingsService = settingsService,
            LibraryService = libraryService,
            Engine = engine,
            Playback = playback,
            Hotkeys = hotkeys,
            Devices = devices,
            UpdateService = updates
        };

        ViewModel = new MainViewModel(settingsService, libraryService, engine, playback, hotkeys, devices, updates);

        ApplyTheme(settingsService.Settings.Theme);

        _mainWindow = new MainWindow(ViewModel);
        MainWindow = _mainWindow;

        if (settingsService.Settings.StartMinimized)
            _mainWindow.Hide(); // tepsiye küçülerek başla
        else
            _mainWindow.Show();

        // motor + kısayollar
        ViewModel.ApplyEngineConfig();
        ViewModel.RegisterAllHotkeys();

        // Güncelleme kontrolü arka planda; açılışı bloklamaz.
        _ = CheckForUpdatesOnStartupAsync();
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            if (ViewModel == null || !ViewModel.Settings.CheckForUpdates) return;

            var last = ViewModel.Settings.LastUpdateCheckUtc;
            if (last is { } lastUtc && DateTime.UtcNow - lastUtc < TimeSpan.FromDays(1))
                return;

            var result = await ViewModel.Updates.CheckAsync(UpdateService.GetCurrentVersion());
            if (result.Status == UpdateCheckStatus.Failed)
                return; // geçici ağ hatasında bir sonraki açılışta tekrar dene

            ViewModel.Settings.LastUpdateCheckUtc = DateTime.UtcNow;
            ViewModel.SaveSettings();
            ViewModel.ApplyUpdateCheckResult(result);
        }
        catch
        {
            // güncelleme kontrolü uygulamayı asla etkilememeli
        }
    }

    public void ApplyTheme(string theme)
    {
        // Tema tek yerde (uygulama kaynaklarında) tutulur. Pencerelerin kendi
        // Theme.Dark sözlüğünü merge etmesi, açık temayı gölgeliyordu.
        var dicts = Resources.MergedDictionaries;
        for (int i = dicts.Count - 1; i >= 0; i--)
        {
            var src = dicts[i].Source?.OriginalString ?? "";
            if (src.Contains("Themes/Theme.Dark.xaml") || src.Contains("Themes/Theme.Light.xaml"))
                dicts.RemoveAt(i);
        }

        var dict = new ResourceDictionary
        {
            Source = new Uri(theme == "light" ? "Themes/Theme.Light.xaml" : "Themes/Theme.Dark.xaml", UriKind.Relative)
        };
        dicts.Add(dict);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SaveSettings();
            ViewModel.Hotkeys.Dispose();
            ViewModel.Dispose();
        }
        if (Paths != null)
        {
            // bekleyen (debounce edilmiş) kütüphane değişikliklerini kaydet
            try { if (Paths.LibraryService.LoadSucceeded) Paths.LibraryService.Save(); } catch { }
            Paths.Engine.Dispose();
            Paths.Devices.Dispose();
            Paths.UpdateService.Dispose();
        }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
