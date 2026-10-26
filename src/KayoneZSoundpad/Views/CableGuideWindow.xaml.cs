using System.Diagnostics;
using System.Windows;
using KayoneZSoundpad.ViewModels;

namespace KayoneZSoundpad.Views;

public partial class CableGuideWindow : Window
{
    private readonly MainViewModel _vm;

    public CableGuideWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
    }

    private void DownloadCable_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://vb-audio.com/Cable/") { UseShellExecute = true }); }
        catch { System.Windows.MessageBox.Show("Tarayıcı açılamadı. Adres: https://vb-audio.com/Cable/", "Voxora"); }
    }

    private void AutoConfig_Click(object sender, RoutedEventArgs e)
    {
        var cableIn = _vm.Devices.FindCableRender();
        var cableOut = _vm.Devices.FindCableCapture();
        if (cableIn == null || cableOut == null)
        {
            System.Windows.MessageBox.Show("Voxora sanal aygıtları bulunamadı. VB-CABLE kurup bilgisayarı yeniden başlat.",
                "Voxora", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_vm.AutoConfigureCable())
        {
            System.Windows.MessageBox.Show("Yönlendirme ayarlanamadı.", "Voxora",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var mic = _vm.CaptureDevices.FirstOrDefault(d => d.Id == _vm.Settings.MicDeviceId);
        var mon = _vm.RenderDevices.FirstOrDefault(d => d.Id == _vm.Settings.MonitorDeviceId);
        var monOn = _vm.Settings.MonitorEnabled;
        var micOn = _vm.Settings.MicEnabled;

        System.Windows.MessageBox.Show(
            "✓ Yönlendirme kuruldu\n\n" +
            "• Çıkış (kablo):  Voxora In Audio\n" +
            "• Mikrofon:       " + (mic?.Name ?? "(bulunamadı)") + (micOn ? "" : "  [KAPALI]") + "\n" +
            "• Lokal dinleme:  " + (mon?.Name ?? "(bulunamadı)") + (monOn ? "" : "  [KAPALI]") + "\n\n" +
            "Şimdi Discord / Valorant gibi uygulamada\n" +
            "GİRİŞ AYGITI olarak \"Voxora Out Audio\" seç.\n\n" +
            "Not: Kendi mikrofonunu Discord'da doğrudan seçersen\n" +
            "efektler duyulmaz — Voxora Out Audio seçmelisin.",
            "Voxora — Mikrofon Yönlendirme Hazır", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenSoundSettings_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }); }
        catch { System.Windows.MessageBox.Show("Ses ayarları açılamadı.", "Voxora"); }
    }
}