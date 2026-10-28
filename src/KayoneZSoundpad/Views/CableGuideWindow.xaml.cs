using System.Diagnostics;
using System.Windows;
using KayoneZSoundpad.Services;
using KayoneZSoundpad.ViewModels;

namespace KayoneZSoundpad.Views;

public partial class CableGuideWindow : Window
{
    private readonly MainViewModel _vm;

    public CableGuideWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        UpdateTexts();
        LocalizationService.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => LocalizationService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged() => UpdateTexts();

    private void UpdateTexts()
    {
        var mic = _vm.GetSelectedCapture()?.Name ?? LocalizationService.T("Guide_NotFound");
        SetOutputText.Text = LocalizationService.T("Guide_SetOutput", "Voxora In Audio");
        SetInputText.Text = LocalizationService.T("Guide_SetInput", mic);
    }

    private void DownloadCable_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://vb-audio.com/Cable/") { UseShellExecute = true }); }
        catch
        {
            System.Windows.MessageBox.Show(LocalizationService.T("Settings_BrowserError"),
                "Voxora", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OpenSoundSettings_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }); }
        catch
        {
            System.Windows.MessageBox.Show(LocalizationService.T("Settings_SoundSettingsError"),
                "Voxora", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void AutoConfig_Click(object sender, RoutedEventArgs e)
    {
        var cableIn = _vm.Devices.FindCableRender();
        var cableOut = _vm.Devices.FindCableCapture();
        if (cableIn == null || cableOut == null)
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

        var s = _vm.Settings;
        var mic = _vm.CaptureDevices.FirstOrDefault(d => d.Id == s.MicDeviceId)?.Name
                  ?? LocalizationService.T("Guide_NotFound");
        var mon = _vm.RenderDevices.FirstOrDefault(d => d.Id == s.MonitorDeviceId)?.Name
                  ?? LocalizationService.T("Guide_NotFound");

        System.Windows.MessageBox.Show(
            LocalizationService.T("Guide_AutoReadyBody",
                mic,
                s.MicEnabled ? "" : LocalizationService.T("Guide_Off"),
                mon,
                s.MonitorEnabled ? "" : LocalizationService.T("Guide_Off")),
            LocalizationService.T("Guide_AutoReadyTitle"),
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
