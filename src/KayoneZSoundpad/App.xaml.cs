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
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // tek örnek
        _singleInstanceMutex = new Mutex(true, "KayoneZSoundpad_SingleInstance", out var isNew);
        if (!isNew)
        {
            System.Windows.MessageBox.Show("Voxora zaten çalışıyor (sistem tepsisine bakın).",
                "Voxora", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // yakalanmayan hata -> gunege yaz, uygulama kapanmasin
        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "voxora-ui.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + " DISPATCHER EXCEPTION: " + args.Exception.GetType().Name + ": " + args.Exception.Message + Environment.NewLine + args.Exception.StackTrace + Environment.NewLine + Environment.NewLine);
            }
            catch { }
            System.Windows.MessageBox.Show($"Beklenmeyen hata: {args.Exception.Message}\n\n{args.Exception.StackTrace}",
                "Voxora - Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppPaths.EnsureDirs();

        var settingsService = new SettingsService();
        var libraryService = new LibraryService();
        var engine = new AudioEngine();
        var playback = new PlaybackManager(engine, settingsService);
        var hotkeys = new GlobalHotkeyService();
        var devices = new DeviceService();

        Paths = new AppPathsHolder
        {
            SettingsService = settingsService,
            LibraryService = libraryService,
            Engine = engine,
            Playback = playback,
            Hotkeys = hotkeys,
            Devices = devices
        };

        ViewModel = new MainViewModel(settingsService, libraryService, engine, playback, hotkeys, devices);

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
    }

    public void ApplyTheme(string theme)
    {
        var dicts = Resources.MergedDictionaries;
        // stiller her zaman Dark'tan gelir; açık temada fırçalar Light ile ezer
        for (int i = dicts.Count - 1; i >= 0; i--)
            if (dicts[i].Source?.OriginalString.Contains("Theme.Light") == true)
                dicts.RemoveAt(i);

        if (theme == "light")
        {
            var light = new ResourceDictionary { Source = new Uri("Themes/Theme.Light.xaml", UriKind.Relative) };
            dicts.Add(light);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SaveSettings();
            ViewModel.Hotkeys.Dispose();
        }
        if (Paths != null)
        {
            Paths.Engine.Dispose();
            Paths.Devices.Dispose();
        }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}


