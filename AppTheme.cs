using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Simultria.RustDeskCompanion;

internal static class AppTheme
{
    internal static readonly Color Canvas = Color.FromArgb(251, 252, 253);
    internal static readonly Color Ink = Color.FromArgb(21, 27, 38);
    internal static readonly Color Muted = Color.FromArgb(91, 105, 125);
    internal static readonly Color Line = Color.FromArgb(224, 229, 236);
    internal static readonly Color Blue = Color.FromArgb(0, 105, 245);
    internal static readonly Color Selection = Color.FromArgb(234, 243, 255);
    internal static readonly Font Body = new("Segoe UI", 10.5F);
    internal static readonly Font Heading = new("Segoe UI Semibold", 20F, FontStyle.Bold);
    internal static readonly Font Strong = new("Segoe UI Semibold", 11F, FontStyle.Bold);
    internal static readonly Font Small = new("Segoe UI", 9.5F);
    internal static readonly Font Caption = new("Segoe UI Semibold", 10.5F);
    internal static readonly Font CaptionSymbol = new("Segoe UI", 15F);

    internal static GraphicsPath Round(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) return path;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static Label Label(string text, Font? font = null, Color? color = null) => new()
    {
        Text = text, Font = font ?? Body, ForeColor = color ?? Ink, AutoSize = true,
        BackColor = Color.Transparent, Margin = Padding.Empty,
    };

    internal static void StyleEditor(Control control)
    {
        control.Font = Body;
        control.Margin = new Padding(0, 5, 0, 12);
        control.AccessibleName ??= control.Name;
        if (control is TextBox box) box.BorderStyle = BorderStyle.FixedSingle;
        if (control is ComboBox combo) combo.FlatStyle = FlatStyle.Flat;
    }
}

internal enum UiGlyph { None, Monitor, Globe, Network, Plus, Trash, Arrow, Info, Check }

internal static class GlyphPainter
{
    internal static void Draw(Graphics graphics, UiGlyph glyph, RectangleF bounds, Color color)
    {
        if (glyph == UiGlyph.None) return;
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TranslateTransform(bounds.X, bounds.Y);
        graphics.ScaleTransform(bounds.Width / 24F, bounds.Height / 24F);
        using var pen = new Pen(color, 1.65F) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        switch (glyph)
        {
            case UiGlyph.Monitor:
                using (var shape = AppTheme.Round(new RectangleF(2, 3, 20, 14), 1.5F)) graphics.DrawPath(pen, shape);
                graphics.DrawLine(pen, 12, 17, 12, 21); graphics.DrawLine(pen, 7, 21, 17, 21); break;
            case UiGlyph.Globe:
                graphics.DrawEllipse(pen, 2, 2, 20, 20); graphics.DrawEllipse(pen, 7, 2, 10, 20);
                graphics.DrawLine(pen, 3, 8, 21, 8); graphics.DrawLine(pen, 3, 16, 21, 16); break;
            case UiGlyph.Network:
                graphics.DrawRectangle(pen, 9, 2, 6, 5); graphics.DrawRectangle(pen, 2, 17, 6, 5);
                graphics.DrawRectangle(pen, 16, 17, 6, 5); graphics.DrawLine(pen, 12, 7, 12, 12);
                graphics.DrawLines(pen, [new Point(5, 17), new Point(5, 12), new Point(19, 12), new Point(19, 17)]); break;
            case UiGlyph.Plus:
                graphics.DrawLine(pen, 12, 3, 12, 21); graphics.DrawLine(pen, 3, 12, 21, 12); break;
            case UiGlyph.Trash:
                graphics.DrawLine(pen, 3, 6, 21, 6); graphics.DrawLines(pen, [new Point(5, 6), new Point(6, 22), new Point(18, 22), new Point(19, 6)]);
                graphics.DrawLines(pen, [new Point(8, 6), new Point(8, 2), new Point(16, 2), new Point(16, 6)]); break;
            case UiGlyph.Arrow:
                graphics.DrawLine(pen, 3, 12, 21, 12); graphics.DrawLines(pen, [new Point(15, 6), new Point(21, 12), new Point(15, 18)]); break;
            case UiGlyph.Info:
                graphics.DrawEllipse(pen, 2, 2, 20, 20); graphics.DrawLine(pen, 12, 11, 12, 17);
                using (var brush = new SolidBrush(color)) graphics.FillEllipse(brush, 11, 6, 2, 2); break;
            case UiGlyph.Check:
                graphics.DrawLines(pen, [new Point(5, 12), new Point(10, 17), new Point(20, 7)]); break;
        }
        graphics.Restore(state);
    }
}

internal sealed class GlyphControl : Control
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal UiGlyph Glyph { get; set; }
    public GlyphControl(UiGlyph glyph)
    {
        Glyph = glyph; ForeColor = AppTheme.Muted; TabStop = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }
    protected override void OnPaint(PaintEventArgs e) => GlyphPainter.Draw(e.Graphics, Glyph, ClientRectangle, ForeColor);
}

internal sealed class ModernButton : Button
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool Primary { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal UiGlyph Glyph { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool GlyphAfter { get; set; }
    private bool hover;
    private bool pressed;

    public ModernButton()
    {
        Font = AppTheme.Body; Cursor = Cursors.Hand; Height = 36; FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Margin = new Padding(0, 0, 8, 0);
    }

    public override Size GetPreferredSize(Size proposedSize) => new(
        TextRenderer.MeasureText(Text, Font).Width + (int)Math.Round((Glyph == UiGlyph.None ? 28 : 52) * DeviceDpi / 96F),
        Math.Max((int)Math.Round(36 * DeviceDpi / 96F), Font.Height + (int)Math.Round(12 * DeviceDpi / 96F)));

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var scale = DeviceDpi / 96F;
        e.Graphics.Clear(Parent?.BackColor ?? AppTheme.Canvas);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var fill = !Enabled ? Color.FromArgb(239, 242, 246) : Primary
            ? (pressed ? Color.FromArgb(0, 77, 196) : hover ? Color.FromArgb(0, 91, 224) : AppTheme.Blue)
            : (pressed ? AppTheme.Selection : hover ? Color.FromArgb(244, 248, 253) : Color.White);
        var ink = !Enabled ? AppTheme.Muted : Primary ? Color.White : AppTheme.Ink;
        using var shape = AppTheme.Round(new RectangleF(.75F, .75F, Width - 1.5F, Height - 1.5F), 7 * scale);
        using var brush = new SolidBrush(fill);
        using var border = new Pen(Primary && Enabled ? fill : Color.FromArgb(167, 181, 201), scale);
        e.Graphics.FillPath(brush, shape); e.Graphics.DrawPath(border, shape);
        var textWidth = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding).Width;
        var iconSize = 18 * scale;
        var gap = Glyph == UiGlyph.None ? 0 : 8 * scale;
        var total = textWidth + (Glyph == UiGlyph.None ? 0 : iconSize + gap);
        var start = (Width - total) / 2;
        if (Glyph != UiGlyph.None)
            GlyphPainter.Draw(e.Graphics, Glyph, new RectangleF(GlyphAfter ? start + textWidth + gap : start, (Height - iconSize) / 2, iconSize, iconSize), ink);
        var textX = start + (Glyph != UiGlyph.None && !GlyphAfter ? iconSize + gap : 0);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle((int)textX, 0, textWidth + 2, Height), ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(Primary ? Color.White : AppTheme.Blue, scale) { DashStyle = DashStyle.Dot };
            using var ring = AppTheme.Round(new RectangleF(4 * scale, 4 * scale, Width - 8 * scale, Height - 8 * scale), 4 * scale);
            e.Graphics.DrawPath(focus, ring);
        }
    }
}

internal sealed class SurfacePanel : Panel
{
    public SurfacePanel()
    {
        DoubleBuffered = true; BackColor = Color.White;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = AppTheme.Round(new RectangleF(.5F, .5F, Width - 1, Height - 1), 10 * DeviceDpi / 96F);
        using var pen = new Pen(AppTheme.Line);
        e.Graphics.DrawPath(pen, path);
    }
}

internal sealed class InputSurface : Panel
{
    private readonly Control editor;
    public InputSurface(Control editor)
    {
        this.editor = editor;
        Height = 36;
        MinimumSize = new Size(100, 36);
        BackColor = Color.White;
        Margin = new Padding(0, 0, 0, 10);
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        if (editor is TextBox box) box.BorderStyle = BorderStyle.None;
        Controls.Add(editor);
        editor.GotFocus += (_, _) => Invalidate();
        editor.LostFocus += (_, _) => Invalidate();
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (editor is null) return;
        var padding = (int)(12 * DeviceDpi / 96F);
        editor.SetBounds(padding, Math.Max(0, (Height - editor.PreferredSize.Height) / 2), Math.Max(1, Width - padding * 2), editor.PreferredSize.Height);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = AppTheme.Round(new RectangleF(.75F, .75F, Width - 1.5F, Height - 1.5F), 7 * DeviceDpi / 96F);
        using var pen = new Pen(ContainsFocus ? AppTheme.Blue : Color.FromArgb(189, 199, 212));
        e.Graphics.DrawPath(pen, shape);
    }
}

internal sealed class Divider : Control
{
    public Divider() { Height = 1; TabStop = false; BackColor = AppTheme.Line; }
}

// Keep the real Windows frame, keyboard/system menu, resizing and taskbar identity,
// but replace the dated title bar with a small, integrated light header.
internal sealed class WindowHeader : Panel
{
    private readonly Form owner;
    private readonly Label title;
    private readonly Button minimize;
    private readonly Button maximize;
    private readonly Button close;
    public WindowHeader(Form owner)
    {
        this.owner = owner;
        DoubleBuffered = true; Height = 40; Dock = DockStyle.Top; BackColor = Color.FromArgb(245, 247, 249);
        var logo = new PictureBox { Name = "TitleBarIcon", Image = AppBranding.Logo, SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(14, 8, 24, 24), TabStop = false };
        title = AppTheme.Label(owner.Text, AppTheme.Caption); title.AutoSize = false;
        title.TextAlign = ContentAlignment.MiddleLeft;
        minimize = CaptionButton("—", "Minimize", () => owner.WindowState = FormWindowState.Minimized);
        maximize = CaptionButton("□", "Maximize or restore", () => owner.WindowState = owner.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized);
        close = CaptionButton("×", "Close", owner.Close);
        Controls.AddRange([logo, title, minimize, maximize, close]);
        owner.TextChanged += (_, _) => title.Text = owner.Text;
        MouseDown += DragWindow; title.MouseDown += DragWindow; logo.MouseDown += DragWindow;
        DoubleClick += ToggleMaximize; title.DoubleClick += ToggleMaximize;
    }
    private Button CaptionButton(string text, string accessibleName, Action action)
    {
        var button = new Button { Text = text, AccessibleName = accessibleName, FlatStyle = FlatStyle.Flat, TabStop = false,
            Font = AppTheme.CaptionSymbol, BackColor = BackColor, ForeColor = AppTheme.Ink, UseVisualStyleBackColor = false };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = text == "×" ? Color.FromArgb(255, 217, 220) : Color.FromArgb(229, 233, 239);
        button.Click += (_, _) => action();
        return button;
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (close is null) return;
        var unit = (int)Math.Round(44 * DeviceDpi / 96F);
        close.SetBounds(Width - unit, 0, unit, Height);
        maximize.Visible = owner.MaximizeBox;
        minimize.Visible = owner.MinimizeBox;
        var next = Width - unit;
        if (maximize.Visible) { next -= unit; maximize.SetBounds(next, 0, unit, Height); }
        if (minimize.Visible) { next -= unit; minimize.SetBounds(next, 0, unit, Height); }
        var left = (int)Math.Round(49 * DeviceDpi / 96F);
        title.SetBounds(left, 0, Math.Max(0, next - left), Height);
    }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using var pen = new Pen(AppTheme.Line); e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1); }
    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        NativeWindowStyle.ReleaseCapture();
        NativeWindowStyle.SendMessage(owner.Handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
    }
    private void ToggleMaximize(object? sender, EventArgs e)
    {
        if (owner.MaximizeBox) maximize.PerformClick();
    }
}

internal static class NativeWindowStyle
{
    [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
    [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);
    [StructLayout(LayoutKind.Sequential)] internal struct Margins { internal int Left, Right, Top, Bottom; }
}
