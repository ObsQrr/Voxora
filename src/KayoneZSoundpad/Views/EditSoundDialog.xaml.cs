using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using KayoneZSoundpad.Models;
using KayoneZSoundpad.ViewModels;

namespace KayoneZSoundpad.Views;

public partial class EditSoundDialog : Window
{
    private static readonly string[] PresetColors =
        { "#7C5CFF", "#FF5C7A", "#4ADE80", "#38BDF8", "#FBBF24", "#F472B6", "#A3E635", "#FB923C", "#94A3B8" };

    private static readonly string[] PresetIcons =
        { "🎵", "🔊", "😂", "💥", "🎮", "😱", "👻", "🔥", "🎺", "📢", "🚨", "💚", "⭐", "💀", "🐴", "🌧️" };

    private readonly MainViewModel _vm;
    private readonly SoundItem _sound;
    private string _selectedColor;

    public EditSoundDialog(MainViewModel vm, SoundItem sound)
    {
        InitializeComponent();
        _vm = vm;
        _sound = sound;
        _selectedColor = string.IsNullOrEmpty(sound.Color) ? PresetColors[0] : sound.Color;

        NameBox.Text = sound.Name;

        // kategoriler: Tümü seçeneği = kategori yok
        var list = new List<object> { new NameOnly("Kategori yok", null) };
        foreach (var c in vm.Library.Data.Categories.OrderBy(c => c.Order))
            list.Add(new NameOnly(c.Name, c.Id));
        CategoryCombo.ItemsSource = list;
        CategoryCombo.SelectedItem = sound.CategoryId == null
            ? list[0]
            : list.FirstOrDefault(x => x is NameOnly n && n.Id == sound.CategoryId) ?? list[0];

        IconCombo.ItemsSource = PresetIcons;
        IconCombo.SelectedItem = string.IsNullOrEmpty(sound.Icon) ? PresetIcons[0] : sound.Icon;

        BuildColorSwatches();

        VolumeSlider.Value = sound.Volume;
        VolumeLabel.Text = $"{Math.Round(sound.Volume * 100)}%";
        VolumeSlider.ValueChanged += (_, e) => VolumeLabel.Text = $"{Math.Round(e.NewValue * 100)}%";

        BehaviorCombo.SelectedIndex = (int)sound.Behavior;
        HoldCheck.IsChecked = sound.HoldToPlay;
        FavCheck.IsChecked = sound.Favorite;
        HotkeyBoxCtl.Value = sound.Hotkey?.Clone();
        HotkeyBoxCtl.LostKeyboardFocus += (_, _) => ValidateHotkey();

        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void BuildColorSwatches()
    {
        ColorPanel.Children.Clear();
        foreach (var hex in PresetColors)
        {
            var border = new Border
            {
                Width = 26, Height = 26, CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)),
                Margin = new Thickness(0, 0, 7, 0), Cursor = System.Windows.Input.Cursors.Hand,
                Tag = hex
            };
            border.MouseDown += (_, _) =>
            {
                _selectedColor = hex;
                UpdateSwatchSelection();
            };
            ColorPanel.Children.Add(border);
        }
        UpdateSwatchSelection();
    }

    private void UpdateSwatchSelection()
    {
        foreach (Border b in ColorPanel.Children)
        {
            bool selected = (string)b.Tag == _selectedColor;
            b.Effect = selected
                ? new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, Color = Colors.White, ShadowDepth = 0 }
                : null;
            b.Opacity = selected ? 1 : 0.75;
        }
    }

    private void ValidateHotkey()
    {
        var hk = HotkeyBoxCtl.Value;
        if (hk?.IsValid != true) { ConflictText.Visibility = Visibility.Collapsed; return; }
        var conflict = _vm.CheckConflict(hk, _sound.Id);
        if (conflict != null)
        {
            ConflictText.Text = "⚠ " + conflict + " — yine de kaydedebilirsin.";
            ConflictText.Visibility = Visibility.Visible;
        }
        else ConflictText.Visibility = Visibility.Collapsed;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0) { NameBox.Focus(); return; }

        _sound.Name = name;
        _sound.CategoryId = (CategoryCombo.SelectedItem as NameOnly)?.Id;
        _sound.Icon = IconCombo.SelectedItem as string;
        _sound.Color = _selectedColor;
        _sound.Volume = (float)VolumeSlider.Value;
        _sound.Behavior = (PlaybackBehavior)BehaviorCombo.SelectedIndex;
        _sound.HoldToPlay = HoldCheck.IsChecked == true;
        _sound.Favorite = FavCheck.IsChecked == true;
        _sound.Hotkey = HotkeyBoxCtl.Value;

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


