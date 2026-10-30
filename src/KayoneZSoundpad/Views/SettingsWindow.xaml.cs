using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Models;
using KayoneZSoundpad.Services;
using KayoneZSoundpad.ViewModels;

namespace KayoneZSoundpad.Views;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly App _app;
    private readonly CancellationTokenSource _updateCts = new();
    private UpdateCheckResult? _lastUpdateResult;
    private bool _updateCheckRunning;
    private bool _closed;

    public SettingsWindow(MainViewModel vm)
    {
        _vm = vm;
        _app = (App)System.Windows.Application.Current;
        InitializeComponent();
        DataContext = _vm;

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

        if (OutputCombo.SelectedItem is DeviceEntry outDev) s.OutputDeviceId = outDev.Id;
        if (MicCombo.SelectedItem is DeviceEntry micDev) s.MicDeviceId = micDev.Id;
        if (MonitorCombo.SelectedItem is DeviceEntry monDev) s.MonitorDeviceId = monDev.Id;

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
