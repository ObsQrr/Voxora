using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KayoneZSoundpad.Models;

namespace KayoneZSoundpad.Views;

/// <summary>
/// Kısayol yakalama kutusu: tıkla → tuş kombinasyonuna bas → Ctrl+Shift+X gibi kaydeder.
/// Esc = temizle. Modifier'a basılıyken gösterir, bırakınca kaydeder.
/// </summary>
public partial class HotkeyBox : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(HotkeyGesture), typeof(HotkeyBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, _) => ((HotkeyBox)d).UpdateDisplay()));

    public HotkeyGesture? Value
    {
        get => (HotkeyGesture?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private HotkeyModifiers _pendingModifiers = HotkeyModifiers.None;

    public HotkeyBox()
    {
        InitializeComponent();
        UpdateDisplay();
        LostKeyboardFocus += (_, _) => { _pendingModifiers = HotkeyModifiers.None; UpdateDisplay(); };
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // sadece modifier: beklemede göster
        if (key is Key.LeftCtrl or Key.RightCtrl) { _pendingModifiers |= HotkeyModifiers.Ctrl; Capture(e); return; }
        if (key is Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin) { _pendingModifiers |= key is Key.LWin or Key.RWin ? HotkeyModifiers.Win : HotkeyModifiers.Alt; Capture(e); return; }
        if (key is Key.LeftShift or Key.RightShift) { _pendingModifiers |= HotkeyModifiers.Shift; Capture(e); return; }

        if (key == Key.Escape)
        {
            Value = null;
            _pendingModifiers = HotkeyModifiers.None;
            UpdateDisplay();
            e.Handled = true;
            return;
        }

        if (key == Key.Back || key == Key.Delete)
        {
            Value = null;
            _pendingModifiers = HotkeyModifiers.None;
            UpdateDisplay();
            e.Handled = true;
            return;
        }

        if (key is Key.Tab or Key.Enter) return; // gezinme

        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0) return;

        var mods = (Keyboard.Modifiers & ModifierKeys.Control) != 0 ? HotkeyModifiers.Ctrl : HotkeyModifiers.None;
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mods |= HotkeyModifiers.Alt;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mods |= HotkeyModifiers.Shift;
        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) mods |= HotkeyModifiers.Win;
        if (mods == HotkeyModifiers.None && _pendingModifiers != HotkeyModifiers.None)
            mods = _pendingModifiers;

        Value = new HotkeyGesture { VirtualKey = vk, Modifiers = mods };
        _pendingModifiers = HotkeyModifiers.None;
        UpdateDisplay();
        e.Handled = true;
    }

    private void Capture(System.Windows.Input.KeyEventArgs e)
    {
        UpdateDisplay(pending: true);
        e.Handled = true;
    }

    private void UpdateDisplay(bool pending = false)
    {
        if (Value?.IsValid == true)
        {
            Display.Text = Value.ToString();
            Display.Foreground = (System.Windows.Media.Brush)(TryFindResource("Brush.Accent") ?? System.Windows.Media.Brushes.White);
            Display.FontWeight = FontWeights.SemiBold;
        }
        else if (pending)
        {
            var parts = new List<string>();
            if (_pendingModifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
            if (_pendingModifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
            if (_pendingModifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
            if (_pendingModifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
            Display.Text = (parts.Count > 0 ? string.Join(" + ", parts) + " + ..." : "...") + " (tuşa bas)";
            Display.Foreground = (System.Windows.Media.Brush)(TryFindResource("Brush.TextSecondary") ?? System.Windows.Media.Brushes.Gray);
        }
        else
        {
            Display.Text = "Tıkla ve kısayol tuşlarına bas (Esc = temizle)";
            Display.Foreground = (System.Windows.Media.Brush)(TryFindResource("Brush.TextMuted") ?? System.Windows.Media.Brushes.Gray);
            Display.FontWeight = FontWeights.Normal;
        }
    }
}


