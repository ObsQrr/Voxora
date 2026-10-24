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
    private bool _suppressDeviceEvents;
    private bool _closingToExit;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        _app = (App)System.Windows.Application.Current;
        DataContext = vm;

        // meter bağlantıları
        FxMeter.SetBinding(System.Windows.Controls.ProgressBar.ValueProperty, new System.Windows.Data.Binding("FxPeak") { Mode = System.Windows.Data.BindingMode.OneWay });
        OutMeter.SetBinding(System.Windows.Controls.ProgressBar.ValueProperty, new System.Windows.Data.Binding("OutPeak") { Mode = System.Windows.Data.BindingMode.OneWay });

        WireViewModel();
        InitTray();
        Loaded += (_, _) =>
        {
            InitMixerBar();
            UpdateEmptyState();
            UpdateCableStatus();
        };

        AllowDrop = true;
        Drop += OnDrop;
        DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
            e.Handled = true;
        };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized && _vm.Settings.CloseToTray) Hide(); };
        Closing += OnClosing;
    }

    private void WireViewModel()
    {
        _vm.AddFilesRequested += AddFilesDialog;
        _vm.AddFolderRequested += AddFolderDialog;
        _vm.AddCategoryRequested += AddCategoryDialog;
        _vm.EditSoundRequested += sound => OpenEditDialog(sound);
        _vm.SettingsRequested += OpenSettingsDialog;
        _vm.FilterApplied += UpdateEmptyState;
        _vm.NotifyInfo += msg => ShowBalloon(msg, Forms.ToolTipIcon.Info);
        _vm.NotifyError += msg => ShowBalloon(msg, Forms.ToolTipIcon.Error);

        _vm.Playback.PlaybackError += (id, msg) => Dispatcher.BeginInvoke(() =>
        {
            ShowBalloon(msg, Forms.ToolTipIcon.Error);
            var card = _vm.GetCard(id);
            card?.RefreshAll();
        });
    }

    // ================================================================ tepsi

    private void InitTray()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "KayoneZ Soundpad",
            Visible = true
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Göster", null, (_, _) => RestoreWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Tüm sesleri durdur", null, (_, _) => _vm.Playback.StopAll());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => RestoreWindow();
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _closingToExit = true;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_vm.Settings.CloseToTray && !_closingToExit)
        {
            e.Cancel = true;
            Hide();
            ShowBalloon("KayoneZ Soundpad tepside çalışmaya devam ediyor.", Forms.ToolTipIcon.Info);
            return;
        }
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
        System.Windows.Application.Current.Shutdown();
    }

    private void ShowBalloon(string message, Forms.ToolTipIcon icon)
    {
        if (_tray != null)
        {
            try { _tray.ShowBalloonTip(3000, "KayoneZ Soundpad", message, icon); return; } catch { }
        }
        // tepsi yoksa (test modu) sessizce geç — yalnız hataları dialogla göster
        if (icon == Forms.ToolTipIcon.Error && !_vm.Settings.CloseToTray)
            System.Windows.MessageBox.Show(this, message, "KayoneZ Soundpad", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    // ================================================================ mikser barı

    private void InitMixerBar()
    {
        _suppressDeviceEvents = true;
        try
        {
            MicToggle.IsChecked = _vm.Settings.MicEnabled;
            MonitorToggle.IsChecked = _vm.Settings.MonitorEnabled;
            MicVolumeSlider.Value = _vm.Settings.MicVolume;
            FxVolumeSlider.Value = _vm.Settings.FxVolume;
            MicVolumeLabel.Text = Pct(_vm.Settings.MicVolume);
            FxVolumeLabel.Text = Pct(_vm.Settings.FxVolume);
            SelectCombo(MicDeviceCombo, _vm.GetSelectedCapture());
            SelectCombo(OutputDeviceCombo, _vm.GetSelectedRender());
        }
        finally { _suppressDeviceEvents = false; }
    }

    private static string Pct(float v) => $"{Math.Round(v * 100)}%";

    private static void SelectCombo(System.Windows.Controls.ComboBox combo, object? item)
    {
        if (item == null) return;
        var idx = combo.Items.IndexOf(item);
        if (idx >= 0) combo.SelectedIndex = idx;
    }

    private void OutputDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressDeviceEvents) return;
        _vm.SetOutputDevice(OutputDeviceCombo.SelectedItem as DeviceEntry);
    }

    private void MicDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressDeviceEvents) return;
        _vm.SetMicDevice(MicDeviceCombo.SelectedItem as DeviceEntry);
    }

    private void MicToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressDeviceEvents) return;
        _vm.SetMicEnabled(MicToggle.IsChecked == true);
    }

    private void MonitorToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressDeviceEvents) return;
        _vm.SetMonitorEnabled(MonitorToggle.IsChecked == true);
    }

    private void MicVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        MicVolumeLabel.Text = Pct((float)e.NewValue);
        _vm.SetMicVolume((float)e.NewValue);
    }

    private void FxVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        FxVolumeLabel.Text = Pct((float)e.NewValue);
        _vm.SetFxVolume((float)e.NewValue);
    }

    private void MixerDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        => _vm.SaveSettings();

    // ================================================================ ses ekleme

    private static readonly string FileDialogFilter =
        "Ses dosyaları|*.mp3;*.wav;*.ogg;*.flac;*.m4a;*.aac;*.wma;*.opus|" +
        "MP3|*.mp3|WAV|*.wav|OGG|*.ogg|FLAC|*.flac|Tüm dosyalar|*.*";

    private Guid? CurrentCategoryId =>
        _vm.SelectedCategory?.Id is Guid id && id != Guid.Empty ? id : null;

    private void AddFilesDialog()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Ses efektleri seç",
            Filter = FileDialogFilter,
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != true) return;
        AddSoundFiles(dlg.FileNames);
    }

    private void AddFolderDialog()
    {
        using var dlg = new Forms.FolderBrowserDialog
        {
            Description = "Klasördeki tüm ses dosyaları eklenir",
            ShowNewFolderButton = false
        };
        if (dlg.ShowDialog() != Forms.DialogResult.OK) return;

        var files = Directory.EnumerateFiles(dlg.SelectedPath, "*", SearchOption.AllDirectories)
            .Where(SoundDecoder.IsSupported)
            .Take(500)
            .ToList();
        if (files.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "Bu klasörde desteklenen ses dosyası bulunamadı.", "KayoneZ",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        AddSoundFiles(files);
    }

    private void AddSoundFiles(IReadOnlyList<string> paths)
    {
        var catId = CurrentCategoryId;
        bool copy = _vm.Settings.CopyOnImport;
        int added = 0, failed = 0;
        var behavior = _vm.Settings.DefaultBehavior;

        foreach (var path in paths)
        {
            try
            {
                if (!SoundDecoder.IsSupported(path)) { failed++; continue; }
                var item = _vm.Library.AddSound(path, catId, copy);
                item.Behavior = behavior;
                _vm.Library.Save();
                _vm.AddSoundToUI(item);
                added++;

                // süreyi ve önbelleği arka planda hazırla
                var itemId = item.Id;
                Task.Run(() =>
                {
                    item.DurationSeconds = SoundDecoder.GetDuration(item.FilePath);
                    _vm.Library.Save();
                    App.Paths!.Engine.TryCache(itemId, item.FilePath, _vm.Settings.MaxCacheMb * 1024L * 1024L);
                    Dispatcher.BeginInvoke(() => _vm.RefreshCard(itemId));
                });
            }
            catch
            {
                failed++;
            }
        }

        if (failed > 0)
            ShowBalloon($"{added} ses eklendi, {failed} dosya eklenemedi.", Forms.ToolTipIcon.Warning);
        else if (added > 0)
            ShowBalloon($"{added} ses eklendi.", Forms.ToolTipIcon.Info);

        UpdateEmptyState();
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        var all = new List<string>();
        foreach (var f in files)
        {
            if (Directory.Exists(f))
                all.AddRange(Directory.EnumerateFiles(f, "*", SearchOption.AllDirectories)
                    .Where(SoundDecoder.IsSupported).Take(500));
            else if (File.Exists(f) && SoundDecoder.IsSupported(f))
                all.Add(f);
        }
        if (all.Count > 0) AddSoundFiles(all);
    }

    // ================================================================ dialoglar

    private void OpenEditDialog(SoundItem sound)
    {
        var dlg = new EditSoundDialog(_vm, sound) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        _vm.Library.Save();
        _vm.RefreshCard(sound.Id);
        var conflicts = _vm.RegisterAllHotkeys();
        if (!string.IsNullOrEmpty(conflicts))
            System.Windows.MessageBox.Show(this, conflicts, "Kısayol çakışması", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OpenSettingsDialog()
    {
        var dlg = new SettingsWindow(_vm) { Owner = this };
        dlg.ShowDialog();
        UpdateCableStatus();
        InitMixerBar();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => OpenSettingsDialog();

    private void OpenCableGuide_Click(object sender, RoutedEventArgs e)
    {
        new CableGuideWindow(_vm) { Owner = this }.ShowDialog();
        UpdateCableStatus();
    }

    private void AddCategoryDialog()
    {
        var dlg = new InputDialog("Kategori ekle", "Kategori adı:") { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Value.Length == 0) return;
        _vm.Library.AddCategory(dlg.Value);
        _vm.RebuildCategories();
    }

    private void RenameCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem mi || mi.Parent is not System.Windows.Controls.ContextMenu cm || cm.PlacementTarget is not ListBoxItem item) return;
        if (item.DataContext is not CategoryEntry entry || entry.Id is not Guid id || id == Guid.Empty) return;
        var dlg = new InputDialog("Kategoriyi yeniden adlandır", "Yeni ad:", entry.Name) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Value.Length == 0) return;
        _vm.Library.RenameCategory(id, dlg.Value);
        _vm.RebuildCategories();
    }

    private void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem mi || mi.Parent is not System.Windows.Controls.ContextMenu cm || cm.PlacementTarget is not ListBoxItem item) return;
        if (item.DataContext is not CategoryEntry entry || entry.Id is not Guid id || id == Guid.Empty) return;
        var result = System.Windows.MessageBox.Show(this, $"\"{entry.Name}\" silinsin mi? İçindeki sesler korunur.",
            "Kategori sil", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        _vm.Library.RemoveCategory(id);
        _vm.RebuildCategories();
        _vm.SelectedCategory = CategoryEntry.All;
    }

    private void UpdateCableStatus()
    {
        var cable = _vm.Devices.FindCableRender() != null && _vm.Devices.FindCableCapture() != null;
        CableStatusIcon.Text = cable ? "✓" : "ℹ";
        CableStatusIcon.Foreground = cable
            ? (System.Windows.Media.Brush)FindResource("Brush.Success")
            : (System.Windows.Media.Brush)FindResource("Brush.Warning");
    }

    private void UpdateEmptyState()
    {
        var visible = _vm.Sounds.Count == 0;
        EmptyState.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    // ================================================================ içe/dışa aktarma

    private void ExportLibrary_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Kütüphaneyi yedekle",
            FileName = $"kayonez-soundpad-{DateTime.Now:yyyyMMdd}.zip",
            Filter = "Kütüphane yedeği (*.zip)|*.zip"
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var importer = new ImportExportService(_vm.Library, _vm.SettingsService);
            int count = importer.Export(dlg.FileName);
            ShowBalloon($"{count} ses yedeklendi: {dlg.FileName}", Forms.ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, "Yedekleme başarısız: " + ex.Message, "KayoneZ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportLibrary_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Kütüphane yedeğini geri yükle",
            Filter = "Kütüphane yedeği (*.zip)|*.zip"
        };
        if (dlg.ShowDialog(this) != true) return;

        if (System.Windows.MessageBox.Show(this, "Yedekteki sesler mevcut kütüphaneye eklenir. Devam?",
                "İçe aktar", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            var importer = new ImportExportService(_vm.Library, _vm.SettingsService);
            int count = importer.Import(dlg.FileName, _vm.Settings.CopyOnImport);
            _vm.ReloadSounds();
            ShowBalloon($"{count} ses içe aktarıldı.", Forms.ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, "İçe aktarma başarısız: " + ex.Message, "KayoneZ",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}







