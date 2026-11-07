using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Features;
using KayoneZSoundpad.Hotkeys;
using KayoneZSoundpad.Services;
using KayoneZSoundpad.ViewModels;
using KayoneZSoundpad.Views;

namespace KayoneZSoundpad;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstanceMutex;

    public App()
    {
        // Başlangıç hatalarını sessizce kaybetme: dispatcher dışında oluşan
        // istisnalar da teşhis günlüğüne yazılsın.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogDiagnostic("APPDOMAIN EXCEPTION: " + e.ExceptionObject);

        // İsteğe bağlı XAML bağlama teşhisi (VOXORA_BINDING_TRACE=1).
        if (Environment.GetEnvironmentVariable("VOXORA_BINDING_TRACE") == "1")
            EnableBindingTrace();
    }

    private static void EnableBindingTrace()
    {
        try
        {
            PresentationTraceSources.Refresh();
            var listener = new TextWriterTraceListener(
                Path.Combine(Path.GetTempPath(), "voxora-bindings.log"));
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
            Trace.AutoFlush = true;
        }
        catch { }
    }

    internal static void LogDiagnostic(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "voxora-ui.log"),
                DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine + Environment.NewLine);
        }
        catch { }
    }

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

        // Yükseltilmiş yardımcı mod: yalnızca cihaz adlarını değiştirip çıkar.
        // Tek örnek mutex'inden ÖNCE ele alınır; aksi halde "zaten çalışıyor" der.
        if (e.Args.Any(a => string.Equals(a, AudioEndpointNamingService.RenameSwitch, StringComparison.OrdinalIgnoreCase)))
        {
            Shutdown(AudioEndpointNamingService.RenameElevated());
            return;
        }

        // Dil, herhangi bir metin üretilmeden önce yüklenmeli (varsayılan: Türkçe).
        LocalizationService.Initialize(LocalizationService.Turkish);

        // tek örnek — farklı veri diziniyle (VOXORA_DATA_DIR) çalışan test/taşınabilir
        // kopya, kurulu uygulamayla çakışmadan açılabilir.
        bool sandboxedProfile = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VOXORA_DATA_DIR"));
        var mutexName = sandboxedProfile
            ? "KayoneZSoundpad_SingleInstance_" + StableProfileKey(AppPaths.BaseDir)
            : "KayoneZSoundpad_SingleInstance";
        _singleInstanceMutex = new Mutex(true, mutexName, out var isNew);
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
        var features = new FeatureService(settingsService);

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

        ViewModel = new MainViewModel(settingsService, libraryService, engine, playback, hotkeys, devices, updates, features);

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

        // İlk kurulum: sihirbaz ana pencere hazır olduktan sonra açılır.
        if (!settingsService.Settings.OnboardingCompleted)
            Dispatcher.BeginInvoke(new Action(() => OpenOnboarding(_mainWindow)),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // Güncelleme kontrolü arka planda; açılışı bloklamaz.
        _ = CheckForUpdatesOnStartupAsync();
    }

    /// <summary>Sandbox profil adı için kararlı (süreçten bağımsız) kısa anahtar.</summary>
    private static string StableProfileKey(string path)
    {
        uint hash = 2166136261;
        foreach (char c in path.ToUpperInvariant())
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash.ToString("X8");
    }

    /// <summary>Kurulum sihirbazını açar (ilk açılış veya Ayarlar'dan).</summary>
    public void OpenOnboarding(Window? owner)
    {
        if (ViewModel == null) return;
        var wizard = new OnboardingWindow(ViewModel, owner ?? _mainWindow);
        wizard.ShowDialog();
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

        // Açık pencerelerin başlık çubuğu da yeni temaya uysun.
        foreach (Window window in Windows)
            WindowChromeHelper.Apply(window);
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
