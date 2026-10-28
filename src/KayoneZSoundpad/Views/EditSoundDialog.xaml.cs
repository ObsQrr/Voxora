using System.IO;
using System.Windows;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Models;
using KayoneZSoundpad.Services;
using KayoneZSoundpad.ViewModels;

namespace KayoneZSoundpad.Views;

public partial class EditSoundDialog : Window
{
    private readonly MainViewModel _vm;
    private readonly SoundItem _sound;

    public EditSoundDialog(MainViewModel vm, SoundItem sound)
    {
        InitializeComponent();
        _vm = vm;
        _sound = sound;

        NameBox.Text = sound.Name;

        // Kategoriler: ilk öğe = kategori yok
        var list = new List<object> { new NameOnly(LocalizationService.T("Category_NoCategory"), null) };
        foreach (var c in vm.Library.Data.Categories.OrderBy(c => c.Order))
            list.Add(new NameOnly(c.Name, c.Id));
        CategoryCombo.ItemsSource = list;
        CategoryCombo.SelectedItem = sound.CategoryId == null
            ? list[0]
            : list.FirstOrDefault(x => x is NameOnly n && n.Id == sound.CategoryId) ?? list[0];

        IconBox.Text = sound.Icon ?? "";
        FilePathBox.Text = sound.FilePath ?? "";

        HotkeyBox.Value = sound.Hotkey?.Clone();
        VolumeSlider.Value = Math.Clamp(sound.Volume, 0f, 1.5f);

        FavoriteCheck.IsChecked = sound.Favorite;
        MutedCheck.IsChecked = sound.IsMuted;

        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void ChangeFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = LocalizationService.T("Dialog_SoundFilesFilter"),
            Title = LocalizationService.T("Dialog_SelectSoundTitle")
        };
        if (dlg.ShowDialog() != true) return;

        if (!File.Exists(dlg.FileName) || !LibraryService.IsSupportedFormat(dlg.FileName))
        {
            System.Windows.MessageBox.Show(LocalizationService.T("Edit_FileInvalid"), "Voxora",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        FilePathBox.Text = dlg.FileName;
    }

    private void ClearHotkey_Click(object sender, RoutedEventArgs e)
    {
        HotkeyBox.Value = null;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0) { NameBox.Focus(); return; }

        if (!File.Exists(FilePathBox.Text) || !LibraryService.IsSupportedFormat(FilePathBox.Text))
        {
            System.Windows.MessageBox.Show(LocalizationService.T("Edit_FileInvalid"), "Voxora",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Kısayol çakışması varsa kullanıcıya sor.
        var gesture = HotkeyBox.Value;
        if (gesture?.IsValid == true)
        {
            var conflict = _vm.CheckConflict(gesture, _sound.Id);
            if (conflict != null &&
                System.Windows.MessageBox.Show(conflict + "\n\n" + LocalizationService.T("Common_Save") + "?",
                    LocalizationService.T("Settings_HotkeyConflictTitle"),
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                HotkeyBox.Focus();
                return;
            }
        }

        _sound.Name = name;
        _sound.CategoryId = (CategoryCombo.SelectedItem as NameOnly)?.Id;
        _sound.Icon = string.IsNullOrWhiteSpace(IconBox.Text) ? null : IconBox.Text.Trim();
        _sound.FilePath = FilePathBox.Text;
        _sound.Volume = Math.Clamp((float)VolumeSlider.Value, 0f, 1.5f);
        _sound.Favorite = FavoriteCheck.IsChecked == true;
        _sound.IsMuted = MutedCheck.IsChecked == true;
        _sound.Hotkey = gesture;

        // Süre değişen dosyaya göre güncellenir (kartta doğru gösterim için).
        try
        {
            var duration = SoundDecoder.GetDuration(_sound.FilePath);
            if (duration > 0) _sound.DurationSeconds = duration;
        }
        catch { /* süre okunamazsa eski değer kalır */ }

        DialogResult = true;
    }

    private class NameOnly
    {
        public string Name { get; }
        public Guid? Id { get; }
        public NameOnly(string name, Guid? id) { Name = name; Id = id; }
        public override string ToString() => Name;
    }
}
