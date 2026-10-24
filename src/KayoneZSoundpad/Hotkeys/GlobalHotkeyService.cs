using System.Diagnostics;
using System.Runtime.InteropServices;
using KayoneZSoundpad.Models;

namespace KayoneZSoundpad.Hotkeys;

/// <summary>
/// WH_KEYBOARD_LL tabanlı global kısayol servisi. Uygulama arka plandayken çalışır,
/// oyunlarda (pencere odağı başka yerdeyken) tuşları yakalar; eşleşen tuşları
/// istenirse sisteme iletilemez (suppress) → oyunda yanlışlıkla tuş basılmaz.
/// </summary>
public class GlobalHotkeyService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr extraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;

    private IntPtr _hook = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc;
    private readonly object _lock = new();
    private readonly Dictionary<HotkeyGesture, Guid> _map = new();
    private readonly HashSet<Guid> _pressed = new();
    private readonly HashSet<int> _downKeys = new();

    /// <summary>Eşleşen tuşlar sisteme iletmesin mi? (oyun içine sızmasın)</summary>
    public bool SuppressMatching { get; set; } = true;

    public event Action<Guid>? HotkeyPressed;   // kalıcı (tek basış) — basılı tutarken bir kez
    public event Action<Guid>? HotkeyReleased;

    public void Start()
    {
        lock (_lock)
        {
            if (_hook != IntPtr.Zero) return;
            _proc = HookProc;
            _hook = SetWindowsHookExW(WH_KEYBOARD_LL, _proc, GetModuleHandleW(null), 0);
            if (_hook == IntPtr.Zero)
                throw new InvalidOperationException($"Klavye hook'u kurulamadı (hata {Marshal.GetLastWin32Error()}).");
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
            _pressed.Clear();
            _downKeys.Clear();
        }
    }

    public void Register(HotkeyGesture gesture, Guid actionId)
    {
        if (gesture == null || !gesture.IsValid) return;
        lock (_lock) _map[gesture] = actionId;
    }

    public void Unregister(HotkeyGesture gesture)
    {
        if (gesture == null) return;
        lock (_lock) _map.Remove(gesture);
    }

    public void UnregisterAction(Guid actionId)
    {
        lock (_lock)
        {
            foreach (var k in _map.Where(kv => kv.Value == actionId).Select(kv => kv.Key).ToList())
                _map.Remove(k);
            _pressed.Remove(actionId);
        }
    }

    public void Clear() { lock (_lock) { _map.Clear(); _pressed.Clear(); } }

    /// <summary>Bu kısayol zaten başka bir eyleme mi atanmış? Atanmışsa eylem kimliğini döner.</summary>
    public Guid? FindOwner(HotkeyGesture gesture)
    {
        lock (_lock) return _map.TryGetValue(gesture, out var id) ? id : null;
    }

    public List<(HotkeyGesture Gesture, Guid Action)> All()
    {
        lock (_lock) return _map.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();
            bool isDown = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
            bool isUp = msg is WM_KEYUP or WM_SYSKEYUP;
            int vk = unchecked((int)info.vkCode);

            if (isDown || isUp)
            {
                if (isDown) _downKeys.Add(vk); else _downKeys.Remove(vk);

                // Modifier tuşlarının kendisi kısayol hedefi değildir
                bool isModifier = vk is VK_SHIFT or VK_CONTROL or VK_MENU or VK_LWIN or VK_RWIN or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;
                if (!isModifier)
                {
                    var mods = ReadModifiers();
                    var gesture = new HotkeyGesture { VirtualKey = vk, Modifiers = mods };
                    Guid action = Guid.Empty;
                    lock (_lock) _map.TryGetValue(gesture, out action);

                    if (action != Guid.Empty)
                    {
                        if (isDown)
                        {
                            bool first = _pressed.Add(action);
                            if (first) HotkeyPressed?.Invoke(action);
                        }
                        else
                        {
                            if (_pressed.Remove(action))
                                HotkeyReleased?.Invoke(action);
                        }
                        if (SuppressMatching) return new IntPtr(1);
                    }
                }
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static HotkeyModifiers ReadModifiers()
    {
        var mods = HotkeyModifiers.None;
        if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0) mods |= HotkeyModifiers.Ctrl;
        if ((GetAsyncKeyState(VK_MENU) & 0x8000) != 0) mods |= HotkeyModifiers.Alt;
        if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0) mods |= HotkeyModifiers.Shift;
        if ((GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0)
            mods |= HotkeyModifiers.Win;
        return mods;
    }

    public void Dispose() => Stop();
}


