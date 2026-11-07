using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Point = System.Windows.Point;

namespace KayoneZSoundpad.Views;

/// <summary>
/// Oyunun/uygulamanın üstünde açılan daire menüsü. Pencere odak çalmaz
/// (WS_EX_NOACTIVATE) — oyun arkada çalışmaya devam eder; seçim fareyle yapılır.
/// </summary>
public partial class RadialMenuWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WhMouseLl = 14;
    private const int WmMouseWheel = 0x020A;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int index, int value);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int idHook, LowLevelMouseProc callback, IntPtr hMod, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? moduleName);

    private delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point32
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookStruct
    {
        public Point32 Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    private readonly Action<object> _onActivate;
    private readonly IReadOnlyList<RadialMenuItem> _allItems;
    private IReadOnlyList<RadialMenuItem> _pageItems = Array.Empty<RadialMenuItem>();
    private readonly DispatcherTimer _autoClose;
    private int _page;
    private bool _closing;
    private LowLevelMouseProc? _mouseProc;
    private IntPtr _mouseHook = IntPtr.Zero;

    public RadialMenuWindow(IReadOnlyList<RadialMenuItem> items, Action<object> onActivate)
    {
        InitializeComponent();
        _onActivate = onActivate;
        _allItems = items;

        ShowPage(0, animate: false);
        Ring.ItemActivated += OnItemActivated;
        Ring.PageRequested += ChangePage;
        Ring.Dismissed += CloseOverlay;

        Root.MouseLeftButtonUp += (_, _) => CloseOverlay();
        Root.MouseRightButtonUp += (_, _) => CloseOverlay();
        PreviewMouseWheel += (_, e) => { ChangePage(e.Delta > 0 ? -1 : 1); e.Handled = true; };

        _autoClose = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _autoClose.Tick += (_, _) => CloseOverlay();

        SourceInitialized += (_, _) =>
        {
            MakeNonActivating();
            InstallMouseHook();
        };
        ContentRendered += (_, _) => PositionAtCursor();
    }

    private void ShowPage(int page, bool animate, int direction = 1)
    {
        int pages = RadialPaging.PageCount(_allItems.Count);
        _page = ((page % pages) + pages) % pages;
        _pageItems = RadialPaging.PageItems(_allItems, _page);
        Ring.SetPaging(_page, pages);
        if (animate) Ring.TransitionTo(_pageItems, direction);
        else Ring.SetItems(_pageItems);
    }

    private void ChangePage(int delta)
    {
        if (RadialPaging.PageCount(_allItems.Count) <= 1) return;
        ShowPage(RadialPaging.Next(_page, delta, _allItems.Count), animate: true, direction: delta);
        _autoClose.Stop();
        _autoClose.Start();
    }

    private void InstallMouseHook()
    {
        try
        {
            _mouseProc = MouseHookProc;
            _mouseHook = SetWindowsHookExW(WhMouseLl, _mouseProc, GetModuleHandleW(null), 0);
            if (_mouseHook == IntPtr.Zero)
                App.LogDiagnostic($"radial mouse hook failed err={Marshal.GetLastWin32Error()}");
        }
        catch (Exception ex)
        {
            App.LogDiagnostic("radial mouse hook failed: " + ex.Message);
            _mouseHook = IntPtr.Zero;
        }
    }

    private IntPtr MouseHookProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam.ToInt32() == WmMouseWheel)
        {
            try
            {
                var info = Marshal.PtrToStructure<MouseHookStruct>(lParam);
                int delta = (short)((info.MouseData >> 16) & 0xFFFF);
                Dispatcher.BeginInvoke(new Action(() => ChangePage(delta > 0 ? -1 : 1)));
            }
            catch { }
            // Menü açıkken tekerlek oyuna gitmesin.
            return new IntPtr(1);
        }
        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private void MakeNonActivating()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(handle, GwlExStyle);
            SetWindowLong(handle, GwlExStyle, style | WsExNoActivate | WsExToolWindow);
        }
        catch { }
    }

    private void PositionAtCursor()
    {
        try
        {
            // Fare konumu pencere koordinatlarında (DIP) doğrudan alınır; DPI
            // dönüşümü gerekmez. Pencere, sanal ekranı kaplar.
            var cursor = Mouse.GetPosition(Root);
            double width = Math.Max(1, Width);
            double height = Math.Max(1, Height);
            double size = Math.Min(600, Math.Min(width, height) - 80);

            Ring.Width = size;
            Ring.Height = size;
            double left = Math.Clamp(cursor.X - size / 2, 6, Math.Max(6, width - size - 6));
            double top = Math.Clamp(cursor.Y - size / 2, 6, Math.Max(6, height - size - 6));
            Canvas.SetLeft(Ring, left);
            Canvas.SetTop(Ring, top);

            PlayOpenAnimation();
        }
        catch
        {
            // Konumlandırma başarısızsa varsayılan merkezde kalır.
        }
    }

    private void PlayOpenAnimation()
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            Opacity = 1;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(170);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var scale = new ScaleTransform(0.92, 0.92);
        Ring.RenderTransformOrigin = new Point(0.5, 0.5);
        Ring.RenderTransform = scale;

        Opacity = 0;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.92, 1, duration) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.92, 1, duration) { EasingFunction = ease });
    }

    private void OnItemActivated(int index)
    {
        try
        {
            if (index >= 0 && index < _pageItems.Count)
                _onActivate(_pageItems[index].Payload);
        }
        finally
        {
            CloseOverlay();
        }
    }

    public void CloseOverlay()
    {
        if (_closing) return;
        _closing = true;
        _autoClose.Stop();

        if (!SystemParameters.ClientAreaAnimation)
        {
            Close();
            return;
        }

        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(110));
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _autoClose.Stop();
        _autoClose.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoClose.Stop();
        if (_mouseHook != IntPtr.Zero)
        {
            try { UnhookWindowsHookEx(_mouseHook); } catch { }
            _mouseHook = IntPtr.Zero;
        }
        _mouseProc = null;
        base.OnClosed(e);
    }
}
