using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Features;
using KayoneZSoundpad.Models;
using KayoneZSoundpad.Services;
using KayoneZSoundpad.ViewModels;

namespace KayoneZSoundpad.Views;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly App _app;
    private readonly CancellationTokenSource _updateCts = new();
    private readonly List<FeatureToggleViewModel> _featureToggles = new();
    private bool _voiceInitializing;
    private UpdateCheckResult? _lastUpdateResult;
    private bool _updateCheckRunning;
    private bool _closed;

    public SettingsWindow(MainViewModel vm)
    {
        _vm = vm;
        _app = (App)System.Windows.Application.Current;
        InitializeComponent();
        DataContext = _vm;
        SourceInitialized += (_, _) => WindowChromeHelper.Apply(this);

        var s = vm.Settings;

        ThemeCombo.SelectedIndex = s.Theme == "light" ? 1 : 0;
        LanguageCombo.SelectedIndex = s.Language == LocalizationService.English ? 1 : 0;
        AutoStartCheck.IsChecked = AutoStartService.IsEnabled();
        StartMinimizedCheck.IsChecked = s.StartMinimized;
        CloseToTrayCheck.IsChecked = s.CloseToTray;
        CopyCheck.IsChecked = s.CopyOnImport;
        BehaviorCombo.SelectedIndex = (int)s.DefaultBehavior;
        SuppressCheck.IsChecked = s.SuppressHotkeys;
        CheckUpdatesCheck.IsChecked = s.CheckForUpdates;
        CurrentVersionText.Text = $"v{VersionText}";

        _voiceInitializing = true;
        VoiceEnabledCheck.IsChecked = s.VoiceChanger.Enabled;
        VoicePresetCombo.SelectedIndex = (int)s.VoiceChanger.Preset;
        VoicePitchSlider.Value = s.VoiceChanger.PitchSemitones;
        VoiceRobotSlider.Value = s.VoiceChanger.RobotMix;
        VoiceEchoSlider.Value = s.VoiceChanger.EchoMix;
        VoiceDelaySlider.Value = s.VoiceChanger.EchoDelayMs;
        VoiceTelephoneCheck.IsChecked = s.VoiceChanger.Telephone;
        _voiceInitializing = false;
        UpdateVoiceLabels();

        foreach (var definition in _vm.Features.Definitions)
            _featureToggles.Add(new FeatureToggleViewModel(_vm.Features, definition));
        FeaturesList.ItemsSource = _featureToggles;

        LatencySlider.Value = s.EngineLatencyMs;
        LatencyLabel.Text = $"{s.EngineLatencyMs} ms";

        CacheSlider.Value = s.MaxCacheMb;
        CacheLabel.Text = $"{s.MaxCacheMb} MB";

        DuckCheck.IsChecked = s.DuckMicWhilePlaying;
        DuckSlider.Value = s.DuckFactor;
        DuckLabel.Text = $"%{Math.Round(s.DuckFactor * 100)}";

        StopAllHotkeyBox.Value = s.StopAllHotkey?.Clone();

        // Cihaz seçimleri elle yönetilir; liste yenilenirken geçici null oluşmaz.
        RefreshDeviceSelections();
        _vm.RenderDevices.CollectionChanged += OnDevicesChanged;
        _vm.CaptureDevices.CollectionChanged += OnDevicesChanged;

        UpdateAbout();
        if (_vm.LastUpdateResult is { } cached)
        {
            _lastUpdateResult = cached;
            ShowUpdateResult(cached);
        }
        Loaded += OnLoaded;
        LocalizationService.LanguageChanged += OnLanguageChanged;
        RailVersionText.Text = $"v{VersionText}";
        NavList.SelectedIndex = 0;
        Closed += (_, _) =>
        {
            _closed = true;
            _updateCts.Cancel();
            LocalizationService.LanguageChanged -= OnLanguageChanged;
            _vm.RenderDevices.CollectionChanged -= OnDevicesChanged;
            _vm.CaptureDevices.CollectionChanged -= OnDevicesChanged;
        };
    }

    private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshDeviceSelections();

    // ------------------------------------------------------------ gezinme

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList?.SelectedItem is not ListBoxItem item) return;

        var tag = item.Tag as string ?? "general";
        FrameworkElement page;
        string titleKey;
        string subtitleKey;

        switch (tag)
        {
            case "audio":
                page = PageAudio; titleKey = "Settings_NavAudio"; subtitleKey = "Settings_PageAudioHint"; break;
            case "behavior":
                page = PageBehavior; titleKey = "Settings_NavBehavior"; subtitleKey = "Settings_PageBehaviorHint"; break;
            case "features":
                page = PageFeatures; titleKey = "Settings_NavFeatures"; subtitleKey = "Settings_PageFeaturesHint"; break;
            case "updates":
                page = PageUpdates; titleKey = "Settings_NavUpdates"; subtitleKey = "Settings_PageUpdatesHint"; break;
            case "about":
                page = PageAbout; titleKey = "Settings_NavAbout"; subtitleKey = "Settings_PageAboutHint"; break;
            default:
                page = PageGeneral; titleKey = "Settings_NavGeneral"; subtitleKey = "Settings_PageGeneralHint"; break;
        }

        foreach (var candidate in new FrameworkElement[] { PageGeneral, PageAudio, PageBehavior, PageFeatures, PageUpdates, PageAbout })
            candidate.Visibility = ReferenceEquals(candidate, page) ? Visibility.Visible : Visibility.Collapsed;

        PageTitle.Text = LocalizationService.T(titleKey);
        PageSubtitle.Text = LocalizationService.T(subtitleKey);
        RevealPage(page);
    }

    /// <summary>Sayfa değişiminde kısa bir giriş: içerik kayarak ve solarak gelir.</summary>
    private static void RevealPage(FrameworkElement page)
    {
        if (!SystemParameters.ClientAreaAnimation) return;

        page.Opacity = 0;
        var shift = new System.Windows.Media.TranslateTransform(0, 8);
        page.RenderTransform = shift;

        var duration = TimeSpan.FromMilliseconds(190);
        var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
        page.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        shift.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new System.Windows.Media.Animation.DoubleAnimation(8, 0, duration) { EasingFunction = ease });
    }

    private void OpenOnboarding_Click(object sender, RoutedEventArgs e)
    {
        var owner = Owner;
        Close();
        _app.OpenOnboarding(owner);
    }

    private async void RenameDevices_Click(object sender, RoutedEventArgs e)
    {
        RenameDevicesButton.IsEnabled = false;
        DeviceNameStatus.Visibility = Visibility.Visible;
        DeviceNameStatus.Foreground = (System.Windows.Media.Brush)FindResource("Brush.TextSecondary");
        DeviceNameStatus.Text = LocalizationService.T("DeviceName_Working");

        try
        {
            var result = await AudioEndpointNamingService.RenameVoxoraEndpointsAsync(this);
            if (_closed) return;

            if (result.Success)
            {
                DeviceNameStatus.Foreground = (System.Windows.Media.Brush)FindResource("Brush.Success");
                DeviceNameStatus.Text = result.AlreadyNamed
                    ? LocalizationService.T("DeviceName_AlreadyNamed")
                    : LocalizationService.T("DeviceName_Done");
            }
            else if (result.Error == null)
            {
                DeviceNameStatus.Visibility = Visibility.Collapsed;
            }
            else
            {
                DeviceNameStatus.Foreground = (System.Windows.Media.Brush)FindResource("Brush.Danger");
                DeviceNameStatus.Text = result.Error;
            }
        }
        finally
        {
            if (!_closed) RenameDevicesButton.IsEnabled = true;
        }
    }

    private void RefreshDeviceSelections()
    {
        if (OutputCombo == null || MicCombo == null || MonitorCombo == null) return;
        var s = _vm.Settings;
        OutputCombo.SelectedValue = s.OutputDeviceId;
        MicCombo.SelectedValue = s.MicDeviceId;
        MonitorCombo.SelectedValue = s.MonitorDeviceId;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await AutoCheckUpdatesAsync();

    private async Task AutoCheckUpdatesAsync()
    {
        if (_vm.LastUpdateResult != null) return;
        if (!_vm.Settings.CheckForUpdates)
        {
            UpdateStatusText.Text = LocalizationService.T("Settings_UpdateDisabled");
            return;
        }

        var last = _vm.Settings.LastUpdateCheckUtc;
        if (last is { } lastUtc && DateTime.UtcNow - lastUtc < TimeSpan.FromDays(1))
        {
            UpdateStatusText.Text = LocalizationService.T("Settings_UpdateNotChecked");
            return;
        }

        await RunUpdateCheckAsync();
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e) => await RunUpdateCheckAsync();

    private async Task RunUpdateCheckAsync()
    {
        if (_updateCheckRunning) return;
        _updateCheckRunning = true;
        CheckUpdatesButton.IsEnabled = false;
        DownloadUpdateButton.Visibility = Visibility.Collapsed;
        UpdateStatusText.Text = LocalizationService.T("Settings_Checking");

        try
        {
            var result = await _vm.Updates.CheckAsync(UpdateService.GetCurrentVersion(), _updateCts.Token);
            if (_closed) return;

            _lastUpdateResult = result;
            if (result.Status != UpdateCheckStatus.Failed)
            {
                _vm.Settings.LastUpdateCheckUtc = DateTime.UtcNow;
                _vm.SaveSettings();
                _vm.ApplyUpdateCheckResult(result);
            }
            ShowUpdateResult(result);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!_closed)
                UpdateStatusText.Text = LocalizationService.T("Settings_UpdateFailed", ex.Message);
        }
        finally
        {
            if (!_closed)
            {
                _updateCheckRunning = false;
                CheckUpdatesButton.IsEnabled = true;
            }
        }
    }

    private void ShowUpdateResult(UpdateCheckResult result)
    {
        if (result.Status == UpdateCheckStatus.UpdateAvailable)
        {
            UpdateStatusText.Text = LocalizationService.T("Settings_UpdateAvailable",
                result.LatestVersion?.ToString(3) ?? "");
            DownloadUpdateButton.Visibility = Visibility.Visible;
        }
        else if (result.Status == UpdateCheckStatus.UpToDate)
        {
            UpdateStatusText.Text = LocalizationService.T("Settings_UpToDate");
            DownloadUpdateButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            UpdateStatusText.Text = LocalizationService.T("Settings_UpdateFailed", result.Error ?? "");
            DownloadUpdateButton.Visibility = Visibility.Collapsed;
        }
    }

    private void DownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        var url = _lastUpdateResult?.ReleaseUrl;
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(LocalizationService.T("Update_BrowserError", ex.Message),
                "Voxora", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private static string VersionText
    {
        get
        {
            var v = typeof(App).Assembly.GetName().Version;
            if (v == null) return "1.2.0";
            return v.Build > 0 ? $"{v.Major}.{v.Minor}.{v.Build}" : $"{v.Major}.{v.Minor}";
        }
    }

    private void UpdateAbout()
    {
        AboutVersionText.Text = $"Voxora v{VersionText}";
        AboutText.Text = LocalizationService.T("Settings_AboutData", VersionText, AppPaths.BaseDir);
    }

    private void OnLanguageChanged()
    {
        if (_closed) return;
        UpdateAbout();
        CurrentVersionText.Text = $"v{VersionText}";
        foreach (var feature in _featureToggles) feature.RefreshText();
        if (_lastUpdateResult != null) ShowUpdateResult(_lastUpdateResult);
        else if (!_updateCheckRunning) UpdateStatusText.Text = LocalizationService.T("Settings_UpdateNotChecked");
        RefreshDeviceSelections();
    }

    private void AutoSetup_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Devices.FindCableRender() == null || _vm.Devices.FindCableCapture() == null)
        {
            System.Windows.MessageBox.Show(LocalizationService.T("Settings_AutoSetupMissing"),
                "Voxora", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_vm.AutoConfigureCable())
        {
            System.Windows.MessageBox.Show(LocalizationService.T("Settings_AutoSetupFailed"),
                "Voxora", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // AppSettings INotifyPropertyChanged değil → combo'ları elle tazele.
        RefreshDeviceSelections();

        var mic = _vm.CaptureDevices.FirstOrDefault(d => d.Id == _vm.Settings.MicDeviceId)?.Name
                  ?? LocalizationService.T("Guide_NotFound");
        var mon = _vm.RenderDevices.FirstOrDefault(d => d.Id == _vm.Settings.MonitorDeviceId)?.Name
                  ?? LocalizationService.T("Guide_NotFound");

        System.Windows.MessageBox.Show(
            LocalizationService.T("Settings_AutoSetupOk", mic, mon),
            "Voxora", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenGuide_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new CableGuideWindow(_vm) { Owner = this };
        dlg.ShowDialog();
    }

    private void LatencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LatencyLabel != null) LatencyLabel.Text = $"{(int)e.NewValue} ms";
    }

    private void CacheSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CacheLabel != null) CacheLabel.Text = $"{(int)e.NewValue} MB";
    }

    private void DuckSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DuckLabel != null) DuckLabel.Text = $"%{Math.Round(e.NewValue * 100)}";
    }

    private void VoicePreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_voiceInitializing) return;
        var preset = SelectedVoicePreset();
        if (preset == VoiceChangerPreset.Custom) return;

        var settings = VoiceChangerSettings.ForPreset(preset);
        VoiceEnabledCheck.IsChecked = settings.Enabled;
        VoicePitchSlider.Value = settings.PitchSemitones;
        VoiceRobotSlider.Value = settings.RobotMix;
        VoiceEchoSlider.Value = settings.EchoMix;
        VoiceDelaySlider.Value = settings.EchoDelayMs;
        VoiceTelephoneCheck.IsChecked = settings.Telephone;
        UpdateVoiceLabels();
    }

    private void VoicePitch_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateVoiceLabels();
    private void VoiceRobot_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateVoiceLabels();
    private void VoiceEcho_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateVoiceLabels();
    private void VoiceDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateVoiceLabels();

    private void VoiceReset_Click(object sender, RoutedEventArgs e)
    {
        var defaults = new VoiceChangerSettings();
        _voiceInitializing = true;
        try
        {
            VoiceEnabledCheck.IsChecked = defaults.Enabled;
            VoicePresetCombo.SelectedIndex = (int)defaults.Preset;
            VoicePitchSlider.Value = defaults.PitchSemitones;
            VoiceRobotSlider.Value = defaults.RobotMix;
            VoiceEchoSlider.Value = defaults.EchoMix;
            VoiceDelaySlider.Value = defaults.EchoDelayMs;
            VoiceTelephoneCheck.IsChecked = defaults.Telephone;
        }
        finally
        {
            _voiceInitializing = false;
        }
        UpdateVoiceLabels();
    }

    private void UpdateVoiceLabels()
    {
        if (VoicePitchLabel != null) VoicePitchLabel.Text = ((int)VoicePitchSlider.Value).ToString();
        if (VoiceRobotLabel != null) VoiceRobotLabel.Text = $"%{Math.Round(VoiceRobotSlider.Value * 100)}";
        if (VoiceEchoLabel != null) VoiceEchoLabel.Text = $"%{Math.Round(VoiceEchoSlider.Value * 100)}";
        if (VoiceDelayLabel != null) VoiceDelayLabel.Text = $"{(int)VoiceDelaySlider.Value} ms";
    }

    private VoiceChangerPreset SelectedVoicePreset()
        => VoicePresetCombo.SelectedItem is ComboBoxItem item
           && Enum.TryParse<VoiceChangerPreset>(item.Tag as string, out var preset)
            ? preset
            : VoiceChangerPreset.Custom;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = _vm.Settings;

        s.Theme = ThemeCombo.SelectedIndex == 1 ? "light" : "dark";
        s.Language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? LocalizationService.Turkish;
        s.StartMinimized = StartMinimizedCheck.IsChecked == true;
        s.CloseToTray = CloseToTrayCheck.IsChecked == true;
        s.CopyOnImport = CopyCheck.IsChecked == true;
        s.DefaultBehavior = (PlaybackBehavior)BehaviorCombo.SelectedIndex;
        s.SuppressHotkeys = SuppressCheck.IsChecked == true;
        s.CheckForUpdates = CheckUpdatesCheck.IsChecked == true;
        s.EngineLatencyMs = (int)LatencySlider.Value;
        s.MaxCacheMb = (int)CacheSlider.Value;
        s.DuckMicWhilePlaying = DuckCheck.IsChecked == true;
        s.DuckFactor = (float)DuckSlider.Value;
        s.StopAllHotkey = StopAllHotkeyBox.Value;

        s.VoiceChanger.Enabled = VoiceEnabledCheck.IsChecked == true;
        s.VoiceChanger.Preset = SelectedVoicePreset();
        s.VoiceChanger.PitchSemitones = (int)VoicePitchSlider.Value;
        s.VoiceChanger.RobotMix = (float)VoiceRobotSlider.Value;
        s.VoiceChanger.EchoMix = (float)VoiceEchoSlider.Value;
        s.VoiceChanger.EchoDelayMs = (int)VoiceDelaySlider.Value;
        s.VoiceChanger.Telephone = VoiceTelephoneCheck.IsChecked == true;
        s.VoiceChanger.Normalize();

        if (OutputCombo.SelectedItem is DeviceEntry outDev) s.OutputDeviceId = outDev.Id;
        if (MicCombo.SelectedItem is DeviceEntry micDev) s.MicDeviceId = micDev.Id;
        if (MonitorCombo.SelectedItem is DeviceEntry monDev) s.MonitorDeviceId = monDev.Id;

        foreach (var feature in _featureToggles)
            feature.Apply();

        AutoStartService.SetEnabled(AutoStartCheck.IsChecked == true);

        _app.ApplyTheme(s.Theme);
        LocalizationService.SetLanguage(s.Language);
        _vm.ApplyEngineConfig();
        var conflicts = _vm.RegisterAllHotkeys();
        _vm.SaveSettings();

        if (!string.IsNullOrEmpty(conflicts))
            System.Windows.MessageBox.Show(conflicts, LocalizationService.T("Settings_HotkeyConflictTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);

        DialogResult = true;
    }
}
