namespace Simultria.RustDeskCompanion;

internal static class AppBranding
{
    internal const string IconResourceName = "RustDeskHop.Assets.RustDeskHop.ico";
    internal const string LogoResourceName = "RustDeskHop.Assets.RustDeskHop.png";

    private static readonly Lazy<Icon> ApplicationIcon = new(() =>
    {
        using var stream = OpenResource(IconResourceName);
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    });

    private static readonly Lazy<Bitmap> ApplicationLogo = new(() =>
    {
        using var stream = OpenResource(LogoResourceName);
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    });

    // Shared for the application's lifetime; individual windows must not dispose these.
    internal static Icon Icon => ApplicationIcon.Value;
    internal static Bitmap Logo => ApplicationLogo.Value;

    private static Stream OpenResource(string name) =>
        typeof(AppBranding).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"Missing application branding resource: {name}");
}

internal abstract class BrandedForm : Form
{
    protected Panel WindowContent { get; } = new() { Dock = DockStyle.Fill };

    protected BrandedForm()
    {
        Icon = AppBranding.Icon;
        ShowIcon = true;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = AppTheme.Body;
        BackColor = AppTheme.Canvas;
        ForeColor = AppTheme.Ink;
        // Expose a narrow form-owned edge so child controls cannot swallow
        // native resize hit tests after extending the client area into the frame.
        Padding = new Padding(6);
        DoubleBuffered = true;
        Controls.Add(WindowContent);
        Controls.Add(new WindowHeader(this));
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var corners = 2;
        NativeWindowStyle.DwmSetWindowAttribute(Handle, 33, ref corners, sizeof(int));
        var margins = new NativeWindowStyle.Margins { Left = 1, Right = 1, Top = 1, Bottom = 1 };
        NativeWindowStyle.DwmExtendFrameIntoClientArea(Handle, ref margins);
    }

    protected override void WndProc(ref Message message)
    {
        const int NonClientCalculate = 0x0083, NonClientHitTest = 0x0084, GetMinMax = 0x0024;
        if (message.Msg == NonClientCalculate && message.WParam != IntPtr.Zero)
        {
            message.Result = IntPtr.Zero;
            return;
        }
        if (message.Msg == NonClientHitTest)
        {
            var point = PointToClient(new Point(unchecked((short)(long)message.LParam), unchecked((short)((long)message.LParam >> 16))));
            var edge = Math.Max(5, (int)(6 * DeviceDpi / 96F));
            var sizable = FormBorderStyle is FormBorderStyle.Sizable or FormBorderStyle.SizableToolWindow;
            var hit = 1;
            if (sizable && WindowState == FormWindowState.Normal)
            {
                var left = point.X < edge; var right = point.X >= ClientSize.Width - edge;
                var top = point.Y < edge; var bottom = point.Y >= ClientSize.Height - edge;
                hit = top ? (left ? 13 : right ? 14 : 12) : bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : right ? 11 : 1;
            }
            message.Result = (IntPtr)hit;
            return;
        }
        base.WndProc(ref message);
        if (message.Msg == GetMinMax)
        {
            var screen = Screen.FromHandle(Handle);
            var info = System.Runtime.InteropServices.Marshal.PtrToStructure<MinMaxInfo>(message.LParam);
            info.MaxPosition = new Point(screen.WorkingArea.Left - screen.Bounds.Left, screen.WorkingArea.Top - screen.Bounds.Top);
            info.MaxSize = screen.WorkingArea.Size;
            System.Runtime.InteropServices.Marshal.StructureToPtr(info, message.LParam, false);
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        internal Point Reserved;
        internal Size MaxSize;
        internal Point MaxPosition;
        internal Size MinTrackSize;
        internal Size MaxTrackSize;
    }
}
