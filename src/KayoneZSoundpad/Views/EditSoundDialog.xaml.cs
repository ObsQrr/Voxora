using System.Windows;
using System.Windows.Controls;
using KayoneZSoundpad.Models;
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

        // Categories: first item = no category
        var list = new List<object> { new NameOnly("Kategori yok", null) };
        foreach (var c in vm.Library.Data.Categories.OrderBy(c => c.Order))
            list.Add(new NameOnly(c.Name, c.Id));
        CategoryCombo.ItemsSource = list;
        CategoryCombo.SelectedItem = sound.CategoryId == null
            ? list[0]
            : list.FirstOrDefault(x => x is NameOnly n && n.Id == sound.CategoryId) ?? list[0];

        IconBox.Text = sound.Icon ?? "";
        FilePathBox.Text = sound.FilePath ?? "";

        HotkeyBox.Value = sound.Hotkey?.Clone();
        VolumeSlider.Value = sound.Volume;

        FavoriteCheck.IsChecked = sound.Favorite;
        MutedCheck.IsChecked = sound.IsMuted;

        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void ChangeFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Ses Dosyaları|*.mp3;*.wav;*.ogg;*.flac|Tüm Dosyalar|*.*",
            Title = "Ses Dosyası Seç"
        };
        if (dlg.ShowDialog() == true)
        {
            FilePathBox.Text = dlg.FileName;
        }
    }

    private void ClearHotkey_Click(object sender, RoutedEventArgs e)
    {
        HotkeyBox.Value = null;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0) { NameBox.Focus(); return; }

        _sound.Name = name;
        _sound.CategoryId = (CategoryCombo.SelectedItem as NameOnly)?.Id;
        _sound.Icon = string.IsNullOrWhiteSpace(IconBox.Text) ? null : IconBox.Text;
        _sound.FilePath = FilePathBox.Text;
        _sound.Volume = (float)VolumeSlider.Value;
        _sound.Favorite = FavoriteCheck.IsChecked == true;
        _sound.IsMuted = MutedCheck.IsChecked == true;
        _sound.Hotkey = HotkeyBox.Value;

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