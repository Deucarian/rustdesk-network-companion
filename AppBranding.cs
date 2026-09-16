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
    protected BrandedForm()
    {
        Icon = AppBranding.Icon;
        ShowIcon = true;
    }
}
