using KayoneZSoundpad.Models;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Services;
using KayoneZSoundpad.ViewModels;

namespace KayoneZSoundpad.Views;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly App _app;

    public SettingsWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        _app = (App)System.Windows.Application.Current;
        var s = vm.Settings;

        ThemeCombo.SelectedIndex = s.Theme == "light" ? 1 : 0;
        AutoStartCheck.IsChecked = AutoStartService.IsEnabled();
        StartMinimizedCheck.IsChecked = s.StartMinimized;
        CloseToTrayCheck.IsChecked = s.CloseToTray;
        CopyCheck.IsChecked = s.CopyOnImport;
        RememberCatCheck.IsChecked = s.RememberLastCategory;
        BehaviorCombo.SelectedIndex = (int)s.DefaultBehavior;
        SuppressCheck.IsChecked = s.SuppressHotkeys;

        LatencySlider.Value = s.EngineLatencyMs;
        LatencyLabel.Text = $"{s.EngineLatencyMs} ms";
        LatencySlider.ValueChanged += (_, e) => LatencyLabel.Text = $"{(int)e.NewValue} ms";

        CacheSlider.Value = s.MaxCacheMb;
        CacheLabel.Text = $"{s.MaxCacheMb} MB";
        CacheSlider.ValueChanged += (_, e) => CacheLabel.Text = $"{(int)e.NewValue} MB";

        DuckCheck.IsChecked = s.DuckMicWhilePlaying;
        DuckSlider.Value = s.DuckFactor;
        DuckLabel.Text = $"%{Math.Round(s.DuckFactor * 100)}";
        DuckSlider.ValueChanged += (_, e) => DuckLabel.Text = $"%{Math.Round(e.NewValue * 100)}";

        MonitorCheck.IsChecked = s.MonitorEnabled;
        StopAllHotkeyBox.Value = s.StopAllHotkey?.Clone();

        Loaded += (_, _) =>
        {
            OutputCombo.SelectedItem = _vm.GetSelectedRender();
            MicCombo.SelectedItem = _vm.GetSelectedCapture();
            MonitorCombo.SelectedItem = _vm.GetSelectedMonitor();
        };

        var cable = _vm.Devices.FindCableCapture();
        CableStatusText.Text = cable != null
            ? "✓ VB-CABLE algılandı — kurulum tamamlanmış görünüyor."
            : "✗ VB-CABLE bulunamadı. Karşı tarafın seni duyması için sanal ses kablosu gerekiyor.";
        AboutText.Text = $"KayoneZ Soundpad v1.0 — NAudio/WASAPI tabanlı ses efekti pedi. " +
                         $"Veriler: {AppPaths.BaseDir}";
    }

    private void DownloadCable_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://vb-audio.com/Cable/") { UseShellExecute = true }); }
        catch { System.Windows.MessageBox.Show("Tarayıcı açılamadı. Adres: https://vb-audio.com/Cable/", "KayoneZ"); }
    }

    private void AutoConfig_Click(object sender, RoutedEventArgs e)
    {
        var cableIn = _vm.Devices.FindCableRender();
        var cableOut = _vm.Devices.FindCableCapture();
        if (cableIn == null || cableOut == null)
        {
            System.Windows.MessageBox.Show("VB-CABLE bulunamadı. Önce kurulum yapıp bilgisayarı yeniden başlat.",
                "KayoneZ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        OutputCombo.SelectedItem = _vm.RenderDevices.FirstOrDefault(d => d.Id == cableIn.Id);
        MicCombo.SelectedItem = _vm.CaptureDevices.FirstOrDefault(d => d.Id == _vm.Settings.MicDeviceId)
                                ?? _vm.CaptureDevices.FirstOrDefault(d => d.IsDefault)
                                ?? _vm.CaptureDevices.FirstOrDefault();
        System.Windows.MessageBox.Show("Çıkış CABLE Input olarak ayarlandı. Discord/oyun tarafında mikrofonu 'CABLE Output' seçmeyi unutma.",
            "KayoneZ", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = _vm.Settings;

        s.Theme = ThemeCombo.SelectedIndex == 1 ? "light" : "dark";
        s.StartMinimized = StartMinimizedCheck.IsChecked == true;
        s.CloseToTray = CloseToTrayCheck.IsChecked == true;
        s.CopyOnImport = CopyCheck.IsChecked == true;
        s.RememberLastCategory = RememberCatCheck.IsChecked == true;
        s.DefaultBehavior = (PlaybackBehavior)BehaviorCombo.SelectedIndex;
        s.SuppressHotkeys = SuppressCheck.IsChecked == true;
        s.EngineLatencyMs = (int)LatencySlider.Value;
        s.MaxCacheMb = (int)CacheSlider.Value;
        s.DuckMicWhilePlaying = DuckCheck.IsChecked == true;
        s.DuckFactor = (float)DuckSlider.Value;
        s.MonitorEnabled = MonitorCheck.IsChecked == true;
        s.StopAllHotkey = StopAllHotkeyBox.Value;

        s.OutputDeviceId = (OutputCombo.SelectedItem as DeviceEntry)?.Id;
        s.MicDeviceId = (MicCombo.SelectedItem as DeviceEntry)?.Id;
        s.MonitorDeviceId = (MonitorCombo.SelectedItem as DeviceEntry)?.Id;

        AutoStartService.SetEnabled(AutoStartCheck.IsChecked == true);

        _app.ApplyTheme(s.Theme);
        _vm.ApplyEngineConfig();
        var conflicts = _vm.RegisterAllHotkeys();
        _vm.SaveSettings();

        if (!string.IsNullOrEmpty(conflicts))
            System.Windows.MessageBox.Show(conflicts, "Kısayol çakışması", MessageBoxButton.OK, MessageBoxImage.Warning);

        DialogResult = true;
    }
}

