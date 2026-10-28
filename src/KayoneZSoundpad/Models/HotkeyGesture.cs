using System.Text.Json.Serialization;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Models;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
    Win = 8
}

/// <summary>
/// Global klavye kısayolu: Win32 sanal tuş kodu + modifiers.
/// JSON'a düz veri olarak kaydedilir; UI bağımlılığı yoktur.
/// </summary>
public class HotkeyGesture : IEquatable<HotkeyGesture>
{
    public HotkeyModifiers Modifiers { get; set; } = HotkeyModifiers.None;
    public int VirtualKey { get; set; }

    [JsonIgnore]
    public bool IsValid => VirtualKey != 0;

    public bool Equals(HotkeyGesture? other) =>
        other is not null && other.VirtualKey == VirtualKey && other.Modifiers == Modifiers;

    public override bool Equals(object? obj) => Equals(obj as HotkeyGesture);

    public override int GetHashCode() => HashCode.Combine(VirtualKey, (int)Modifiers);

    public override string ToString()
    {
        if (!IsValid) return "";
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(VkName(VirtualKey));
        return string.Join(" + ", parts);
    }

    public HotkeyGesture Clone() => new() { Modifiers = Modifiers, VirtualKey = VirtualKey };

    private static string VkName(int vk) => vk switch
    {
        0x01 => LocalizationService.T("Key_LeftMouse"),
        0x02 => LocalizationService.T("Key_RightMouse"),
        0x04 => LocalizationService.T("Key_MiddleMouse"),
        0x25 => LocalizationService.T("Key_Left"),
        0x26 => LocalizationService.T("Key_Up"),
        0x27 => LocalizationService.T("Key_Right"),
        0x28 => LocalizationService.T("Key_Down"),
        0x5B => LocalizationService.T("Key_LeftWin"),
        0x5C => LocalizationService.T("Key_RightWin"),
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),                  // 0-9
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),                  // A-Z
        >= 0x60 and <= 0x69 => LocalizationService.T("Key_NumPrefix") + (vk - 0x60), // Numpad0-9
        >= 0x70 and <= 0x87 => "F" + (vk - 0x70 + 1),                  // F1-F24
        _ => VkTable.TryGetValue(vk, out var n) ? n : "0x" + vk.ToString("X2")
    };

    private static readonly Dictionary<int, string> VkTable = new()
    {
        [0x08] = "Backspace", [0x09] = "Tab", [0x0D] = "Enter",
        [0x10] = "Shift", [0x11] = "Ctrl", [0x12] = "Alt", [0x14] = "Caps Lock",
        [0x1B] = "Esc", [0x20] = "Space", [0x21] = "PgUp", [0x22] = "PgDn",
        [0x23] = "End", [0x24] = "Home", [0x25] = "Sol", [0x26] = "Yukarı",
        [0x27] = "Sağ", [0x28] = "Aşağı", [0x2D] = "Insert", [0x2E] = "Delete",
        [0x5B] = "Sol Win", [0x5C] = "Sağ Win", [0x6A] = "Num *", [0x6B] = "Num +",
        [0x6D] = "Num -", [0x6E] = "Num .", [0x6F] = "Num /",
        [0xBA] = ";", [0xBB] = "=", [0xBC] = ",", [0xBD] = "-", [0xBE] = ".",
        [0xBF] = "/", [0xC0] = "`", [0xDB] = "[", [0xDC] = "\\", [0xDD] = "]", [0xDE] = "'",
        [0xDF] = "F13", [0x90] = "Num Lock", [0x91] = "Scroll Lock"
    };
}
