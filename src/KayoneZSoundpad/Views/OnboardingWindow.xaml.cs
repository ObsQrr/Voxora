using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Services;
using KayoneZSoundpad.ViewModels;
using Brush = System.Windows.Media.Brush;

namespace KayoneZSoundpad.Views;

/// <summary>
/// Kurulum sonrası ilk açılış sihirbazı: dil, tema, ses yönlendirmesi ve
/// Windows cihaz adları. Tüm seçimler anında uygulanır (canlı önizleme),
/// dosyaya yalnızca bitirince yazılır.
/// </summary>
public partial class OnboardingWindow : Window
{
    private const int StepCount = 5;

    private readonly MainViewModel _vm;
    private readonly App _app;
    private readonly FrameworkElement[] _panels;
    private int _step;
    private bool _ready;
    private bool _completed;
    private string _pendingTheme;

    public OnboardingWindow(MainViewModel vm, Window? owner = null)
    {
        _vm = vm;
        _app = (App)System.Windows.Application.Current;
        _pendingTheme = string.Equals(vm.Settings.Theme, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark";

        InitializeComponent();
        DataContext = vm;
        if (owner != null) Owner = owner;

        _panels = new FrameworkElement[] { StepWelcome, StepTheme, StepAudio, StepDevice, StepReady };

        _ready = false;
        SelectDevices();
        MonitorSwitch.IsChecked = vm.Settings.MonitorEnabled;
        _ready = true;

        UpdateLanguageMarks();
        UpdateThemeCards();
        ShowStep(0, animate: false);
        LocalizationService.LanguageChanged += OnLanguageChanged;

        Loaded += (_, _) =>
        {
            UpdateSummary();
            PlayEntrance();
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
        Closed += (_, _) => LocalizationService.LanguageChanged -= OnLanguageChanged;
    }

    private void PlayEntrance()
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        var duration = TimeSpan.FromMilliseconds(260);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Opacity = 0;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
    }

    // ------------------------------------------------------------ adımlar

    private void ShowStep(int step, bool animate = true, int direction = 1)
    {
        _step = Math.Clamp(step, 0, StepCount - 1);

        for (int i = 0; i < _panels.Length; i++)
            _panels[i].Visibility = i == _step ? Visibility.Visible : Visibility.Collapsed;

        var panel = _panels[_step];
        if (animate && SystemParameters.ClientAreaAnimation)
        {
            panel.Opacity = 0;
            var shift = new TranslateTransform(26 * direction, 0);
            panel.RenderTransform = shift;

            var duration = TimeSpan.FromMilliseconds(220);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            panel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
            shift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(26 * direction, 0, duration) { EasingFunction = ease });
        }
        else
        {
            panel.Opacity = 1;
            panel.RenderTransform = Transform.Identity;
        }

        BackButton.Visibility = _step == 0 ? Visibility.Collapsed : Visibility.Visible;
        NextButton.Content = _step == StepCount - 1
            ? LocalizationService.T("Onboarding_Finish")
            : LocalizationService.T("Onboarding_Next");

        StepLabel.Text = LocalizationService.T("Onboarding_StepOf", _step + 1, StepCount);
        UpdateDots();

        if (_step == StepCount - 1) UpdateSummary();
    }

    private void UpdateDots()
    {
        var activeBrush = (Brush)FindResource("Brush.Accent");
        var idleBrush = (Brush)FindResource("Brush.Divider");
        for (int i = 0; i < Dots.Children.Count; i++)
        {
            if (Dots.Children[i] is not Border dot) continue;
            bool active = i == _step;
            dot.Width = active ? 18 : 5;
            dot.Background = active ? activeBrush : idleBrush;
        }

        if (SystemParameters.ClientAreaAnimation && Dots.Children[_step] is Border current)
        {
            var widthAnimation = new DoubleAnimation(current.Width, 18, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            current.BeginAnimation(WidthProperty, widthAnimation);
        }
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_step >= StepCount - 1)
        {
            Complete();
            return;
        }
        ShowStep(_step + 1, direction: 1);
    }

    private void Back_Click(object sender, RoutedEventArgs e) => ShowStep(_step - 1, direction: -1);

    private void Close_Click(object sender, RoutedEventArgs e) => Complete();

    private void Complete()
    {
        if (_completed) return;
        _completed = true;

        var settings = _vm.Settings;
        settings.Theme = _pendingTheme;
        settings.Language = LocalizationService.Language;
        settings.OnboardingCompleted = true;
        settings.MonitorEnabled = MonitorSwitch.IsChecked == true;

        _app.ApplyTheme(settings.Theme);
        _vm.ApplyEngineConfig();
        _vm.SaveSettings();

        try { DialogResult = true; } catch { /* modal değilse yok say */ }
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!_completed)
        {
            // Kapatmak da bir tercihtir: sihirbaz her açılışta tekrar etmesin.
            _vm.Settings.OnboardingCompleted = true;
            _vm.Settings.Theme = _pendingTheme;
            _vm.Settings.Language = LocalizationService.Language;
            _vm.SaveSettings();
        }
        base.OnClosed(e);
    }

    // ------------------------------------------------------------ dil

    private void LangTr_Click(object sender, RoutedEventArgs e) => SwitchLanguage(LocalizationService.Turkish);
    private void LangEn_Click(object sender, RoutedEventArgs e) => SwitchLanguage(LocalizationService.English);

    private void SwitchLanguage(string language)
    {
        LocalizationService.SetLanguage(language);
        UpdateLanguageMarks();
    }

    private void UpdateLanguageMarks()
    {
        bool turkish = LocalizationService.Language != LocalizationService.English;
        LangTrCheck.Visibility = turkish ? Visibility.Visible : Visibility.Collapsed;
        LangEnCheck.Visibility = turkish ? Visibility.Collapsed : Visibility.Visible;
        LangTrButton.BorderBrush = turkish
            ? (Brush)FindResource("Brush.Accent")
            : (Brush)FindResource("Brush.InputBorder");
        LangEnButton.BorderBrush = turkish
            ? (Brush)FindResource("Brush.InputBorder")
            : (Brush)FindResource("Brush.Accent");
        StepLabel.Text = LocalizationService.T("Onboarding_StepOf", _step + 1, StepCount);
        NextButton.Content = _step == StepCount - 1
            ? LocalizationService.T("Onboarding_Finish")
            : LocalizationService.T("Onboarding_Next");
        UpdateSummary();
    }

    private void OnLanguageChanged()
    {
        UpdateLanguageMarks();
        UpdateSummary();
    }

    // ------------------------------------------------------------ tema

    private void ThemeDark_Click(object sender, RoutedEventArgs e) => ApplyThemePreview("dark");
    private void ThemeLight_Click(object sender, RoutedEventArgs e) => ApplyThemePreview("light");

    private void ApplyThemePreview(string theme)
    {
        _pendingTheme = theme;
        _app.ApplyTheme(theme);
        UpdateThemeCards();
    }

    private void UpdateThemeCards()
    {
        var accent = (Brush)FindResource("Brush.Accent");
        var idle = (Brush)FindResource("Brush.Divider");
        bool dark = _pendingTheme != "light";
        ThemeDarkCard.BorderBrush = dark ? accent : idle;
        ThemeLightCard.BorderBrush = dark ? idle : accent;
        ThemeDarkCard.Opacity = dark ? 1 : 0.82;
        ThemeLightCard.Opacity = dark ? 0.82 : 1;
    }

    // ------------------------------------------------------------ ses

    private void SelectDevices()
    {
        var settings = _vm.Settings;
        OutputCombo.SelectedValue = settings.OutputDeviceId;
        MicCombo.SelectedValue = settings.MicDeviceId;
    }

    private void Output_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || OutputCombo?.SelectedItem is not DeviceEntry device) return;
        _vm.SetOutputDevice(device);
        UpdateSummary();
    }

    private void Mic_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || MicCombo?.SelectedItem is not DeviceEntry device) return;
        _vm.SetMicDevice(device);
        UpdateSummary();
    }

    private void Monitor_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _vm.SetMonitorEnabled(MonitorSwitch.IsChecked == true);
    }

    private void TestTone_Click(object sender, RoutedEventArgs e)
    {
        bool played = _vm.Engine.PlayTestTone();
        TestToneStatus.Text = played
            ? LocalizationService.T("Onboarding_TestTonePlayed")
            : LocalizationService.T("Onboarding_TestToneFailed");
        TestToneStatus.Foreground = played
            ? (Brush)FindResource("Brush.Success")
            : (Brush)FindResource("Brush.Danger");
    }

    private void AutoSetup_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.AutoConfigureCable())
        {
            System.Windows.MessageBox.Show(LocalizationService.T("Settings_AutoSetupMissing"),
                "Voxora", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _ready = false;
        SelectDevices();
        _ready = true;
        UpdateSummary();
    }

    // ------------------------------------------------------------ cihaz adı

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        RenameStatus.Visibility = Visibility.Visible;
        RenameStatus.Foreground = (Brush)FindResource("Brush.TextSecondary");
        RenameStatus.Text = LocalizationService.T("DeviceName_Working");

        var result = await AudioEndpointNamingService.RenameVoxoraEndpointsAsync(this);
        if (!IsLoaded) return;

        if (result.Success)
        {
            RenameStatus.Foreground = (Brush)FindResource("Brush.Success");
            RenameStatus.Text = result.AlreadyNamed
                ? LocalizationService.T("DeviceName_AlreadyNamed")
                : LocalizationService.T("DeviceName_Done");

            _vm.RefreshDevices();
            _ready = false;
            SelectDevices();
            _ready = true;
            UpdateSummary();
        }
        else if (result.Error == null)
        {
            RenameStatus.Visibility = Visibility.Collapsed;
        }
        else
        {
            RenameStatus.Foreground = (Brush)FindResource("Brush.Danger");
            RenameStatus.Text = result.Error;
        }
    }

    private void SkipDevice_Click(object sender, RoutedEventArgs e) => ShowStep(_step + 1, direction: 1);

    // ------------------------------------------------------------ özet

    private void UpdateSummary()
    {
        if (SummaryOutput == null || SummaryMic == null) return;

        var output = _vm.RenderDevices.FirstOrDefault(d => d.Id == _vm.Settings.OutputDeviceId)
                     ?? _vm.RenderDevices.FirstOrDefault(d => d.IsCable);
        var mic = _vm.CaptureDevices.FirstOrDefault(d => d.Id == _vm.Settings.MicDeviceId)
                  ?? _vm.CaptureDevices.FirstOrDefault();

        SummaryOutput.Text = LocalizationService.T("Onboarding_SummaryAudio", output?.Name ?? "-");
        SummaryMic.Text = LocalizationService.T("Onboarding_SummaryMic", mic?.Name ?? "-");
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        try { DragMove(); } catch { }
    }
}
