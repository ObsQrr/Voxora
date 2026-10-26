using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Models;
using KayoneZSoundpad.Services;
using KayoneZSoundpad.ViewModels;
using Forms = System.Windows.Forms;

namespace KayoneZSoundpad.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly App _app;
    private Forms.NotifyIcon? _tray;
    private bool _closingToExit;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        _app = (App)System.Windows.Application.Current;
        DataContext = vm;

        WireViewModel();
        InitTray();
    }

    private void WireViewModel()
    {
        _vm.AddFilesRequested += AddFilesDialog;
        _vm.AddFolderRequested += AddFolderDialog;
        _vm.AddCategoryRequested += AddCategoryDialog;
        _vm.EditSoundRequested += sound => OpenEditDialog(sound);
        _vm.SettingsRequested += OpenSettingsDialog;
        _vm.NotifyInfo += msg => ShowBalloon(msg, Forms.ToolTipIcon.Info);
        _vm.NotifyError += msg => ShowBalloon(msg, Forms.ToolTipIcon.Error);

        _vm.Playback.PlaybackError += (id, msg) => Dispatcher.BeginInvoke(() =>
        {
            ShowBalloon(msg, Forms.ToolTipIcon.Error);
        });
    }

    private void AddFilesDialog()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Ses Dosyaları|*.mp3;*.wav;*.ogg;*.flac|Tüm Dosyalar|*.*",
            Multiselect = true,
            Title = "Ses Dosyası Seç"
        };
        if (dlg.ShowDialog() == true)
            _vm.AddFilesCommand.Execute(dlg.FileNames);
    }

    private void AddFolderDialog()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Klasör Seç" };
        if (dlg.ShowDialog() == true)
            _vm.AddFolderCommand.Execute(dlg.FolderName);
    }

    private void AddCategoryDialog()
    {
        var dlg = new InputDialog("Yeni Kategori", "Kategori adı:", "");
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Value))
            _vm.AddCategoryCommand.Execute(dlg.Value.Trim());
    }

    private void OpenEditDialog(SoundItem sound)
    {
        var dlg = new EditSoundDialog(_vm, sound) { Owner = this };
        if (dlg.ShowDialog() == true)
            _vm.Library.Save();
    }

    private void OpenSettingsDialog()
    {
        var dlg = new SettingsWindow(_vm) { Owner = this };
        dlg.ShowDialog();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => OpenSettingsDialog();

    private void OpenCableGuide_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new CableGuideWindow(_vm) { Owner = this };
        dlg.ShowDialog();
    }

    private void ImportLibrary_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Voxora Yedek|*.zip",
            Title = "Kütüphane Yedeğini Seç"
        };
        if (dlg.ShowDialog() == true)
        {
            try { _vm.ImportLibraryCommand.Execute(dlg.FileName); }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "Voxora", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }

    private void ExportLibrary_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Voxora Yedek|*.zip",
            FileName = $"voxora-library-{DateTime.Now:yyyyMMdd}.zip",
            Title = "Kütüphaneyi Yedekle"
        };
        if (dlg.ShowDialog() == true)
        {
            try { _vm.ExportLibraryCommand.Execute(dlg.FileName); }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "Voxora", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }

    private void RenameCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is Category cat)
        {
            var dlg = new InputDialog("Kategori Yeniden Adlandır", "Yeni ad:", cat.Name);
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Value))
            {
                _vm.Library.RenameCategory(cat.Id, dlg.Value.Trim());
            }
        }
    }

    private void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is Category cat)
        {
            if (System.Windows.MessageBox.Show($"'{cat.Name}' kategorisini ve içindeki sesleri sil?", "Voxora",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _vm.Library.RemoveCategory(cat.Id);
                _vm.RebuildCategories();
            }
        }
    }

    private void MicToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (MicToggle == null) return;
        _vm.SetMicEnabled(MicToggle.IsChecked == true);
    }

    private void MonitorToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (MonitorToggle == null) return;
        _vm.SetMonitorEnabled(MonitorToggle.IsChecked == true);
    }

    private void MicDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MicDeviceCombo?.SelectedItem is DeviceEntry d)
            _vm.Settings.MicDeviceId = d.Id;
    }

    private void OutputDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OutputDeviceCombo?.SelectedItem is DeviceEntry d)
            _vm.Settings.MonitorDeviceId = d.Id;
    }

    private void MicVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MicVolumeLabel == null) return;
        var v = (float)e.NewValue;
        _vm.SetMicVolume(v);
        MicVolumeLabel.Text = $"{Math.Round(v * 100)}%";
    }

    private void FxVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FxVolumeLabel == null) return;
        var v = (float)e.NewValue;
        _vm.SetFxVolume(v);
        FxVolumeLabel.Text = $"{Math.Round(v * 100)}%";
    }

    private void MixerDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        _vm.SaveSettings();
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            var audio = files.Where(f => ".mp3.wav.ogg.flac".Contains(System.IO.Path.GetExtension(f).ToLower())).ToArray();
            if (audio.Length > 0) _vm.AddFilesCommand.Execute(audio);
        }
    }

    private void InitTray()
    {
        var iconStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("KayoneZSoundpad.app.ico");
        _tray = new Forms.NotifyIcon
        {
            Icon = iconStream != null ? new System.Drawing.Icon(iconStream) : System.Drawing.SystemIcons.Application,
            Text = "Voxora",
            Visible = true
        };
        _tray.DoubleClick += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Göster", null, (_, _) => Dispatcher.BeginInvoke(() => { Show(); WindowState = WindowState.Normal; Activate(); }));
        menu.Items.Add("Çıkış", null, (_, _) => Dispatcher.BeginInvoke(() => { _closingToExit = true; Close(); }));
        _tray.ContextMenuStrip = menu;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized && _vm.Settings.CloseToTray)
        {
            Hide();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_closingToExit && _vm.Settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            _tray?.Dispose();
        }
    }

    private void ShowBalloon(string msg, Forms.ToolTipIcon icon)
    {
        _tray?.ShowBalloonTip(3000, "Voxora", msg, icon);
    }
}