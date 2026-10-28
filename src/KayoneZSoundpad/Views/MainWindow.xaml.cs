using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
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
    private Forms.ContextMenuStrip? _trayMenu;
    private bool _closingToExit;
    private bool _uiReady;
    private readonly DispatcherTimer _meterTimer;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        _app = (App)System.Windows.Application.Current;
        DataContext = vm;

        WireViewModel();
        InitTray();

        // Cihaz listesi her yenilendiğinde seçimi kayıtlı ID'ye göre geri koy.
        _vm.RenderDevices.CollectionChanged += (_, _) => RefreshDeviceSelections();
        _vm.CaptureDevices.CollectionChanged += (_, _) => RefreshDeviceSelections();

        LocalizationService.LanguageChanged += OnLanguageChanged;

        // ayarları UI'a yansıt (event'lerin erken tetiklenmesini engelle)
        Loaded += (_, _) => SyncUiFromSettings();

        // seviye göstergeleri
        _meterTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _meterTimer.Tick += (_, _) =>
        {
            FxMeter.Value = _vm.FxPeak;
            OutMeter.Value = _vm.OutPeak;
        };
        _meterTimer.Start();
    }

    private void WireViewModel()
    {
        _vm.AddFilesRequested += AddFilesDialog;
        _vm.AddFolderRequested += AddFolderDialog;
        _vm.AddCategoryRequested += AddCategoryDialog;
        _vm.EditSoundRequested += sound => OpenEditDialog(sound);
        _vm.SettingsRequested += OpenSettingsDialog;
        _vm.CableGuideRequested += OpenCableGuide;
        _vm.NotifyInfo += msg => ShowBalloon(msg, Forms.ToolTipIcon.Info);
        _vm.NotifyError += msg => ShowBalloon(msg, Forms.ToolTipIcon.Error);
        _vm.FilterApplied += UpdateEmptyState;
        _vm.Sounds.CollectionChanged += (_, _) => UpdateEmptyState();

        _vm.Playback.PlaybackError += (id, msg) => Dispatcher.BeginInvoke(() =>
        {
            ShowBalloon(msg, Forms.ToolTipIcon.Error);
        });
    }

    private void SyncUiFromSettings()
    {
        var s = _vm.Settings;
        MicVolumeSlider.Value = s.MicVolume;
        FxVolumeSlider.Value = s.FxVolume;
        MicVolumeLabel.Text = $"{Math.Round(s.MicVolume * 100)}%";
        FxVolumeLabel.Text = $"{Math.Round(s.FxVolume * 100)}%";
        MicToggle.IsChecked = s.MicEnabled;
        MonitorToggle.IsChecked = s.MonitorEnabled;
        RefreshDeviceSelections();
        _uiReady = true;
        UpdateEmptyState();
    }

    /// <summary>
    /// Cihaz listesi yenilendiğinde ComboBox seçimini kayıtlı ID'ye göre geri koyar.
    /// SelectedValue'yu Settings'e iki yönlü bağlamıyoruz: liste temizlenirken
    /// oluşan geçici null, kayıtlı cihazı siliyordu.
    /// </summary>
    private void RefreshDeviceSelections()
    {
        if (MicDeviceCombo == null || OutputDeviceCombo == null) return;

        var ready = _uiReady;
        _uiReady = false;
        try
        {
            var s = _vm.Settings;
            if (!Equals(MicDeviceCombo.SelectedValue, s.MicDeviceId))
                MicDeviceCombo.SelectedValue = s.MicDeviceId;
            if (!Equals(OutputDeviceCombo.SelectedValue, s.OutputDeviceId))
                OutputDeviceCombo.SelectedValue = s.OutputDeviceId;
        }
        finally
        {
            _uiReady = ready;
        }
    }

    private void UpdateEmptyState()
    {
        var view = CollectionViewSource.GetDefaultView(_vm.Sounds);
        EmptyState.Visibility = view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddFilesDialog()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = LocalizationService.T("Dialog_SoundFilesFilter"),
            Multiselect = true,
            Title = LocalizationService.T("Dialog_SelectSoundTitle")
        };
        if (dlg.ShowDialog() == true)
            _vm.AddFiles(dlg.FileNames);
    }

    private void AddFolderDialog()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = LocalizationService.T("Dialog_SelectFolderTitle") };
        if (dlg.ShowDialog() == true)
            _vm.AddFolder(dlg.FolderName);
    }

    private void AddCategoryDialog()
    {
        var dlg = new InputDialog(
            LocalizationService.T("Category_NewTitle"),
            LocalizationService.T("Category_NamePrompt"), "") { Owner = this };
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Value))
            _vm.AddCategory(dlg.Value.Trim());
    }

    private void OpenEditDialog(SoundItem sound)
    {
        var oldPath = sound.FilePath;
        var dlg = new EditSoundDialog(_vm, sound) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            if (!string.Equals(oldPath, sound.FilePath, StringComparison.OrdinalIgnoreCase))
                _vm.Playback.EvictCache(sound.Id);

            _vm.Library.Save();
            _vm.RefreshCard(sound.Id);

            var conflict = _vm.RegisterAllHotkeys();
            if (!string.IsNullOrEmpty(conflict))
                System.Windows.MessageBox.Show(conflict, LocalizationService.T("Settings_HotkeyConflictTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenSettingsDialog()
    {
        var dlg = new SettingsWindow(_vm) { Owner = this };
        dlg.ShowDialog();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => OpenSettingsDialog();

    private void OpenCableGuide_Click(object sender, RoutedEventArgs e) => OpenCableGuide();

    private void OpenCableGuide()
    {
        var dlg = new CableGuideWindow(_vm) { Owner = this };
        dlg.ShowDialog();
    }

    private void ImportLibrary_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = LocalizationService.T("Dialog_BackupFilter"),
            Title = LocalizationService.T("Dialog_SelectBackupTitle")
        };
        if (dlg.ShowDialog() == true)
            _vm.ImportLibrary(dlg.FileName);
    }

    private void ExportLibrary_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = LocalizationService.T("Dialog_BackupFilter"),
            FileName = $"voxora-library-{DateTime.Now:yyyyMMdd}.zip",
            Title = LocalizationService.T("Dialog_SaveBackupTitle")
        };
        if (dlg.ShowDialog() == true)
            _vm.ExportLibrary(dlg.FileName);
    }

    private void CategoryList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // Sağ tıklanan öğeyi seç: menü her zaman tıklanan kategoriye uygulanmalı.
        if (e.OriginalSource is ListBoxItem item && item.DataContext is CategoryEntry clicked)
            CategoryList.SelectedItem = clicked;

        // "Tüm Sesler" ve "Favoriler" yeniden adlandırılamaz/silinemez.
        if (_vm.SelectedCategory == null || _vm.SelectedCategory.Id is null || _vm.SelectedCategory.Id == Guid.Empty)
            e.Handled = true;
    }

    private void CardMenu_Click(object sender, RoutedEventArgs e)
    {
        // ContextMenu ayrı bir görsel ağaçta yaşadığı için komutlar Window
        // DataContext'ine bağlanamıyordu; eylemleri buradan yönlendiriyoruz.
        if (sender is not MenuItem mi || mi.DataContext is not SoundCardViewModel card) return;

        switch (mi.Tag as string)
        {
            case "play": _vm.PlayStopCommand.Execute(card); break;
            case "mute": _vm.ToggleMuteCommand.Execute(card); break;
            case "edit": _vm.EditSoundCommand.Execute(card); break;
            case "favorite": _vm.ToggleFavoriteCommand.Execute(card); break;
            case "remove": _vm.DeleteSoundCommand.Execute(card); break;
        }
    }

    private void RenameCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is CategoryEntry entry && entry.Id is Guid id && id != Guid.Empty)
        {
            var dlg = new InputDialog(
                LocalizationService.T("Category_RenameTitle"),
                LocalizationService.T("Category_RenamePrompt"), entry.Name) { Owner = this };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Value))
            {
                _vm.Library.RenameCategory(id, dlg.Value.Trim());
                _vm.RebuildCategories();
            }
        }
    }

    private void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is CategoryEntry entry && entry.Id is Guid id && id != Guid.Empty)
        {
            if (System.Windows.MessageBox.Show(
                    LocalizationService.T("Category_DeleteConfirm", entry.Name),
                    "Voxora", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _vm.Library.RemoveCategory(id);
                _vm.RebuildCategories();
                _vm.SelectedCategory = _vm.Categories.FirstOrDefault();
            }
        }
    }

    private void MicToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || MicToggle == null) return;
        _vm.SetMicEnabled(MicToggle.IsChecked == true);
    }

    private void MonitorToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || MonitorToggle == null) return;
        _vm.SetMonitorEnabled(MonitorToggle.IsChecked == true);
    }

    private void MicDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady) return;
        if (MicDeviceCombo?.SelectedItem is DeviceEntry d)
            _vm.SetMicDevice(d);
    }

    private void OutputDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady) return;
        if (OutputDeviceCombo?.SelectedItem is DeviceEntry d)
            _vm.SetOutputDevice(d);
    }

    private void MicVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_uiReady || MicVolumeLabel == null) return;
        var v = (float)e.NewValue;
        _vm.SetMicVolume(v);
        MicVolumeLabel.Text = $"{Math.Round(v * 100)}%";
    }

    private void FxVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_uiReady || FxVolumeLabel == null) return;
        var v = (float)e.NewValue;
        _vm.SetFxVolume(v);
        FxVolumeLabel.Text = $"{Math.Round(v * 100)}%";
    }

    private void MixerDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        _vm.SaveSettings();
    }

    private void OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return;

        var paths = (string[]?)e.Data.GetData(System.Windows.DataFormats.FileDrop);
        if (paths == null) return;

        var audio = paths.Where(p => File.Exists(p) && LibraryService.IsSupportedFormat(p)).ToArray();
        if (audio.Length > 0) _vm.AddFiles(audio);

        foreach (var dir in paths.Where(Directory.Exists))
            _vm.AddFolder(dir);

        e.Handled = true;
    }

    private void InitTray()
    {
        var iconStream = System.Reflection.Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("KayoneZSoundpad.app.ico");
        _tray = new Forms.NotifyIcon
        {
            Icon = iconStream != null ? new System.Drawing.Icon(iconStream) : System.Drawing.SystemIcons.Application,
            Text = "Voxora",
            Visible = true
        };
        _tray.DoubleClick += (_, _) => Dispatcher.BeginInvoke(ShowFromTray);
        BuildTrayMenu();
    }

    private void BuildTrayMenu()
    {
        if (_tray == null) return;

        _trayMenu?.Dispose();
        _trayMenu = new Forms.ContextMenuStrip();
        _trayMenu.Items.Add(LocalizationService.T("Tray_Show"), null,
            (_, _) => Dispatcher.BeginInvoke(ShowFromTray));
        _trayMenu.Items.Add(LocalizationService.T("Tray_Exit"), null,
            (_, _) => Dispatcher.BeginInvoke(() => { _closingToExit = true; Close(); }));
        _tray.ContextMenuStrip = _trayMenu;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OnLanguageChanged()
    {
        BuildTrayMenu();
        UpdateEmptyState();
        RefreshDeviceSelections();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized && _vm.Settings.CloseToTray)
        {
            Hide();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_closingToExit && _vm.Settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _meterTimer.Stop();
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        _tray?.Dispose();
        _tray = null;
        base.OnClosed(e);

        // ShutdownMode = OnExplicitShutdown: pencere kapanınca süreç arkada kalmasın.
        System.Windows.Application.Current.Shutdown();
    }

    private void ShowBalloon(string msg, Forms.ToolTipIcon icon)
    {
        _tray?.ShowBalloonTip(3000, "Voxora", msg, icon);
    }
}
