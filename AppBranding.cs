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
    protected Panel WindowContent { get; } = new() { Name = "WindowContent", Dock = DockStyle.Fill };

    protected BrandedForm()
    {
        Icon = AppBranding.Icon;
        ShowIcon = true;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = AppTheme.Body;
        BackColor = AppTheme.Canvas;
        ForeColor = AppTheme.Ink;
        // Keep the actual non-client area. Windows owns caption buttons, hit testing,
        // accessibility, snap layouts, system menus and per-monitor frame sizing.
        Padding = Padding.Empty;
        DoubleBuffered = true;
        Controls.Add(WindowContent);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyWindowTheme();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        ApplyWindowTheme(true);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        ApplyWindowTheme(false);
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        // A theme/accessibility change must not leave forced caption colours behind.
        if (message.Msg is 0x001A or 0x031A) ApplyWindowTheme();
    }

    private void ApplyWindowTheme(bool? active = null)
    {
        if (IsHandleCreated) NativeWindowTheme.Apply(Handle, active ?? ActiveForm == this);
    }
}
