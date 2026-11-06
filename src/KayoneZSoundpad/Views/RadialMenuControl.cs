using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using KayoneZSoundpad.Services;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Pen = System.Windows.Media.Pen;
using Size = System.Windows.Size;
using FontFamily = System.Windows.Media.FontFamily;

namespace KayoneZSoundpad.Views;

/// <summary>Daire menüsündeki tek dilim.</summary>
public sealed record RadialMenuItem(string Title, string Subtitle, object Payload);

/// <summary>
/// Oyunun üstünde açılan daire menüsü. Segmentler fareyle seçilir; metinler
/// kesilmeden sığdırılır. Tema renkleri her çizimde canlı okunur.
/// </summary>
public sealed class RadialMenuControl : FrameworkElement
{
    private IReadOnlyList<RadialMenuItem> _items = Array.Empty<RadialMenuItem>();
    private int _hover = -1;
    private double _openProgress = 1;

    public event Action<int>? ItemActivated;
    public event Action? Dismissed;

    public RadialMenuControl()
    {
        Focusable = false;
        SnapsToDevicePixels = true;
    }

    public void SetItems(IReadOnlyList<RadialMenuItem> items)
    {
        _items = items;
        _hover = -1;
        InvalidateVisual();
    }

    public void SetOpenProgress(double progress)
    {
        _openProgress = Math.Clamp(progress, 0, 1);
        InvalidateVisual();
    }

    private double OuterRadius => Math.Min(ActualWidth, ActualHeight) / 2 - 12;
    private double InnerRadius => Math.Max(64, OuterRadius * 0.46);

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double alpha = _openProgress;
        if (alpha <= 0.01) return;

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        double outer = OuterRadius * (0.92 + 0.08 * alpha);
        double inner = InnerRadius;
        if (outer <= inner + 10) return;

        var accent = Resource("Brush.Accent", Color.FromRgb(0xFF, 0x5C, 0x35));
        var panel = Resource("Brush.Surface", Color.FromRgb(0x1A, 0x16, 0x12));
        var divider = Resource("Brush.Divider", Color.FromRgb(0x2C, 0x25, 0x1E));
        var textPrimary = Resource("Brush.TextPrimary", Color.FromRgb(0xF4, 0xED, 0xE3));
        var textMuted = Resource("Brush.TextMuted", Color.FromRgb(0x7F, 0x72, 0x63));

        dc.PushOpacity(alpha);

        if (_items.Count == 0)
        {
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0xF2, 0x1A, 0x16, 0x12)),
                new Pen(new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0x5C, 0x35)), 1),
                center, inner + 34, inner + 34);
            DrawCentered(dc, LocalizationService.T("Radial_Empty"), center, 14, textMuted, 220);
            dc.Pop();
            return;
        }

        int count = _items.Count;
        double step = 360.0 / count;
        double gap = count > 1 ? Math.Min(2.6, step * 0.06) : 0;

        for (int i = 0; i < count; i++)
        {
            double start = -90 + i * step + gap / 2;
            double end = start + step - gap;
            bool hovered = i == _hover;

            var geometry = BuildSegment(center, inner, outer, start, end);
            Color fillColor = hovered
                ? Color.FromArgb(0xF2, accent.R, accent.G, accent.B)
                : Color.FromArgb(0xEA, panel.R, panel.G, panel.B);
            var fill = new SolidColorBrush(fillColor);
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(hovered ? (byte)0xFF : (byte)0x66,
                divider.R, divider.G, divider.B)), hovered ? 1.6 : 1);
            dc.DrawGeometry(fill, pen, geometry);

            double mid = (start + end) / 2 * Math.PI / 180.0;
            double radius = (inner + outer) / 2;
            var anchor = new Point(center.X + Math.Cos(mid) * radius, center.Y + Math.Sin(mid) * radius);

            double maxWidth = count == 1
                ? outer * 0.9
                : Math.Max(70, 2 * radius * Math.Sin(step * Math.PI / 360.0) * 0.9);

            var brush = hovered ? Colors.White : textPrimary;
            DrawCentered(dc, _items[i].Title, anchor, 13, brush, maxWidth, FontWeights.SemiBold, 2);

            if (!string.IsNullOrWhiteSpace(_items[i].Subtitle))
                DrawCentered(dc, _items[i].Subtitle,
                    new Point(anchor.X, anchor.Y + 24), 10,
                    hovered ? Colors.White : textMuted, maxWidth);
        }

        // merkez disk
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0xF5, 0x10, 0x0E, 0x0C)),
            new Pen(new SolidColorBrush(Color.FromArgb(0x55, divider.R, divider.G, divider.B)), 1),
            center, inner - 8, inner - 8);
        DrawCentered(dc, "VOXORA", new Point(center.X, center.Y - 10), 15, accent, inner * 1.4, FontWeights.SemiBold);
        DrawCentered(dc, LocalizationService.T("Radial_CloseHint"), new Point(center.X, center.Y + 13), 10,
            textMuted, inner * 1.5);

        dc.Pop();
    }

    private void DrawCentered(DrawingContext dc, string text, Point center, double size, Color color,
        double maxWidth, FontWeight? weight = null, int maxLines = 1)
    {
        if (string.IsNullOrEmpty(text)) return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI, Arial"),
                FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal),
            size,
            new SolidColorBrush(color),
            dpi)
        {
            MaxTextWidth = Math.Max(40, maxWidth),
            MaxLineCount = maxLines,
            Trimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center
        };

        dc.DrawText(formatted, new Point(center.X - formatted.Width / 2, center.Y - formatted.Height / 2));
    }

    private static StreamGeometry BuildSegment(Point center, double inner, double outer, double startDeg, double endDeg)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var p1 = Polar(center, outer, startDeg);
            var p2 = Polar(center, outer, endDeg);
            var p3 = Polar(center, inner, endDeg);
            var p4 = Polar(center, inner, startDeg);

            ctx.BeginFigure(p1, true, true);
            ctx.ArcTo(p2, new Size(outer, outer), 0, endDeg - startDeg > 180, SweepDirection.Clockwise,
                true, false);
            ctx.LineTo(p3, true, false);
            ctx.ArcTo(p4, new Size(inner, inner), 0, endDeg - startDeg > 180, SweepDirection.Counterclockwise,
                true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Point Polar(Point center, double radius, double degrees)
    {
        double rad = degrees * Math.PI / 180.0;
        return new Point(center.X + Math.Cos(rad) * radius, center.Y + Math.Sin(rad) * radius);
    }

    private Color Resource(string key, Color fallback)
        => TryFindResource(key) is SolidColorBrush brush ? brush.Color : fallback;

    // ------------------------------------------------------------------ fare

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int index = HitTest(e.GetPosition(this));
        if (index == _hover) return;

        _hover = index;
        Cursor = index >= 0 ? Cursors.Hand : Cursors.Arrow;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover == -1) return;
        _hover = -1;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        int index = HitTest(e.GetPosition(this));
        if (index >= 0)
        {
            e.Handled = true;
            ItemActivated?.Invoke(index);
        }
        else
        {
            e.Handled = true;
            Dismissed?.Invoke();
        }
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        e.Handled = true;
        Dismissed?.Invoke();
    }

    private int HitTest(Point point)
    {
        if (_items.Count == 0) return -1;

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        double dx = point.X - center.X;
        double dy = point.Y - center.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance < InnerRadius || distance > OuterRadius) return -1;

        double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        double relative = (angle + 90 + 360) % 360;
        int index = (int)(relative / (360.0 / _items.Count));
        return Math.Clamp(index, 0, _items.Count - 1);
    }
}
