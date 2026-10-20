using System.Drawing.Drawing2D;

namespace CamoSwitcher.App;

internal static class GlassDrawing
{
    internal static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        radius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2);
        var d = Math.Max(1, radius * 2);
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class GlassPanel : Panel
{
    private readonly int _radius;
    private readonly bool _shadow;
    internal GlassPanel(int radius = 25, bool shadow = true)
    {
        _radius = radius;
        _shadow = shadow;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Dock = DockStyle.Fill;
        Padding = new Padding(18);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 10 || Height < 10) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (_shadow)
        {
            using var shadow = GlassDrawing.Rounded(new RectangleF(3, 6, Width - 6, Height - 7), _radius);
            using var ink = new SolidBrush(Color.FromArgb(15, 54, 77, 123));
            e.Graphics.FillPath(ink, shadow);
        }
        var bounds = new RectangleF(2, 2, Width - 5, Height - 7);
        using var path = GlassDrawing.Rounded(bounds, _radius);
        using var fill = new LinearGradientBrush(bounds, Color.FromArgb(235, 255, 255, 255), Color.FromArgb(190, 249, 252, 255), 85);
        using var border = new Pen(Color.FromArgb(235, 255, 255, 255), 1.2f);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
        base.OnPaint(e);
    }
}

internal sealed class GlassButton : Button
{
    private readonly bool _primary;
    private bool _active;
    private bool _hovered;
    private bool _pressed;
    internal GlassButton(bool primary)
    {
        _primary = primary;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
    }
    internal void SetActive(bool active) { if (_active == active) return; _active = active; Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 4 || Height < 4) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var blue = (_primary || _active) && Enabled;
        var top = !Enabled ? Color.FromArgb(230, 235, 244) : blue ? Color.FromArgb(66, 160, 255) : Color.FromArgb(252, 254, 255);
        var bottom = !Enabled ? Color.FromArgb(225, 232, 242) : blue ? Color.FromArgb(0, 112, 244) : Color.FromArgb(228, 237, 250);
        if (_hovered && Enabled) bottom = ControlPaint.Light(bottom, .1f);
        if (_pressed && Enabled) bottom = ControlPaint.Dark(bottom, .1f);
        var bounds = new RectangleF(1, 1, Width - 3, Height - 3);
        using var shape = GlassDrawing.Rounded(bounds, Math.Min(20, Height / 2f));
        using var fill = new LinearGradientBrush(bounds, top, bottom, 90);
        using var border = new Pen(Color.FromArgb(210, 255, 255, 255));
        e.Graphics.FillPath(fill, shape);
        e.Graphics.DrawPath(border, shape);
        var ink = !Enabled ? Color.FromArgb(129, 144, 165) : blue ? Color.White : Color.FromArgb(40, 66, 107);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform);
        if (Enabled && Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -7, -7), ink, BackColor);
    }
}

internal sealed class SmoothGrid : DataGridView
{
    internal SmoothGrid() => DoubleBuffered = true;
}
