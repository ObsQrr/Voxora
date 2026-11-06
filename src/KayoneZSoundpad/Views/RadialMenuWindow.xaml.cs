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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int index, int value);

    private readonly Action<object> _onActivate;
    private readonly IReadOnlyList<RadialMenuItem> _items;
    private readonly DispatcherTimer _autoClose;
    private bool _closing;

    public RadialMenuWindow(IReadOnlyList<RadialMenuItem> items, Action<object> onActivate)
    {
        InitializeComponent();
        _onActivate = onActivate;
        _items = items;

        Ring.SetItems(items);
        Ring.ItemActivated += OnItemActivated;
        Ring.Dismissed += CloseOverlay;

        Root.MouseLeftButtonUp += (_, _) => CloseOverlay();
        Root.MouseRightButtonUp += (_, _) => CloseOverlay();

        _autoClose = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _autoClose.Tick += (_, _) => CloseOverlay();

        SourceInitialized += (_, _) => MakeNonActivating();
        ContentRendered += (_, _) => PositionAtCursor();
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
            if (index >= 0 && index < _items.Count)
                _onActivate(_items[index].Payload);
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
        base.OnClosed(e);
    }
}
