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
        _vm = vm;
        _app = (App)System.Windows.Application.Current;
        DataContext = _vm;
        InitializeComponent();
        var s = vm.Settings;

        ThemeCombo.SelectedIndex = s.Theme == "light" ? 1 : 0;
        AutoStartCheck.IsChecked = AutoStartService.IsEnabled();
        StartMinimizedCheck.IsChecked = s.StartMinimized;
        CloseToTrayCheck.IsChecked = s.CloseToTray;
        CopyCheck.IsChecked = s.CopyOnImport;
        BehaviorCombo.SelectedIndex = (int)s.DefaultBehavior;
        SuppressCheck.IsChecked = s.SuppressHotkeys;

        LatencySlider.Value = s.EngineLatencyMs;
        LatencyLabel.Text = $"{s.EngineLatencyMs} ms";

        CacheSlider.Value = s.MaxCacheMb;
        CacheLabel.Text = $"{s.MaxCacheMb} MB";

        DuckCheck.IsChecked = s.DuckMicWhilePlaying;
        DuckSlider.Value = s.DuckFactor;
        DuckLabel.Text = $"%{Math.Round(s.DuckFactor * 100)}";

        StopAllHotkeyBox.Value = s.StopAllHotkey?.Clone();

        // SelectedValue binding (SelectedValuePath=Id) handles device selection automatically
        Loaded += (_, _) =>
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                Log("SettingsWindow Loaded (deferred) basladi");
                Log("Settings.OutputDeviceId = " + (_vm.Settings.OutputDeviceId ?? "null"));
                Log("Settings.MicDeviceId = " + (_vm.Settings.MicDeviceId ?? "null"));
                Log("Settings.MonitorDeviceId = " + (_vm.Settings.MonitorDeviceId ?? "null"));
                Log("RenderDevices count = " + _vm.RenderDevices.Count);
                Log("CaptureDevices count = " + _vm.CaptureDevices.Count);
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        };

        AboutText.Text = $"Voxora v1.1 — NAudio/WASAPI tabanlı ses efekti pedi. " +
                         $"Veriler: {AppPaths.BaseDir}";
    }

    private void Log(string msg)
    {
        try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "voxora-ui.log"),
            DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + Environment.NewLine); } catch { }
    }

    private void DownloadCable_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://vb-audio.com/Cable/") { UseShellExecute = true }); }
        catch { System.Windows.MessageBox.Show("Tarayıcı açılamadı. Adres: https://vb-audio.com/Cable/", "Voxora"); }
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
        Log("Save_Click basladi");
        Log("OutputCombo.SelectedItem = " + ((OutputCombo.SelectedItem as DeviceEntry)?.Id ?? "null"));
        Log("MicCombo.SelectedItem = " + ((MicCombo.SelectedItem as DeviceEntry)?.Id ?? "null"));
        Log("MonitorCombo.SelectedItem = " + ((MonitorCombo.SelectedItem as DeviceEntry)?.Id ?? "null"));

        var s = _vm.Settings;

        s.Theme = ThemeCombo.SelectedIndex == 1 ? "light" : "dark";
        s.StartMinimized = StartMinimizedCheck.IsChecked == true;
        s.CloseToTray = CloseToTrayCheck.IsChecked == true;
        s.CopyOnImport = CopyCheck.IsChecked == true;
        s.DefaultBehavior = (PlaybackBehavior)BehaviorCombo.SelectedIndex;
        s.SuppressHotkeys = SuppressCheck.IsChecked == true;
        s.EngineLatencyMs = (int)LatencySlider.Value;
        s.MaxCacheMb = (int)CacheSlider.Value;
        s.DuckMicWhilePlaying = DuckCheck.IsChecked == true;
        s.DuckFactor = (float)DuckSlider.Value;
        s.StopAllHotkey = StopAllHotkeyBox.Value;

        // SelectedValue binding handles these automatically, but keep fallback
        s.OutputDeviceId = (OutputCombo.SelectedItem as DeviceEntry)?.Id;
        s.MicDeviceId = (MicCombo.SelectedItem as DeviceEntry)?.Id;
        s.MonitorDeviceId = (MonitorCombo.SelectedItem as DeviceEntry)?.Id;

        AutoStartService.SetEnabled(AutoStartCheck.IsChecked == true);

        _app.ApplyTheme(s.Theme);
        _vm.ApplyEngineConfig();
        var conflicts = _vm.RegisterAllHotkeys();
        _vm.SaveSettings();
        Log("SaveSettings cagirildi");

        if (!string.IsNullOrEmpty(conflicts))
            System.Windows.MessageBox.Show(conflicts, "Kısayol çakışması", MessageBoxButton.OK, MessageBoxImage.Warning);

        DialogResult = true;
    }
}