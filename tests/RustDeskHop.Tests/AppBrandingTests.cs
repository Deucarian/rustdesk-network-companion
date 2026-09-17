using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Simultria.RustDeskCompanion;
using Xunit;

namespace RustDeskHop.Tests;

public sealed class AppBrandingTests
{
    [Fact]
    public void IconResourceContainsAllWindowsSizes()
    {
        using var stream = typeof(AppBranding).Assembly.GetManifestResourceStream(AppBranding.IconResourceName);
        Assert.NotNull(stream);
        using var reader = new BinaryReader(stream);
        Assert.Equal(0, reader.ReadUInt16());
        Assert.Equal(1, reader.ReadUInt16());
        var count = reader.ReadUInt16();
        var sizes = new List<int>();
        for (var index = 0; index < count; index++)
        {
            var width = reader.ReadByte();
            var height = reader.ReadByte();
            Assert.Equal(width, height);
            sizes.Add(width == 0 ? 256 : width);
            reader.ReadBytes(14);
        }
        Assert.Equal(new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 }, sizes);
    }

    [Fact]
    public void EmbeddedArtworkLoadsWithoutExternalFiles()
    {
        Assert.NotEqual(IntPtr.Zero, AppBranding.Icon.Handle);
        var logo = AppBranding.Logo;
        Assert.Equal(logo.Width, logo.Height);
        Assert.True(logo.Width >= 256);
        Assert.Equal(0, logo.GetPixel(0, 0).A);
        Assert.Equal(0, logo.GetPixel(logo.Width - 1, logo.Height - 1).A);
        Assert.True(logo.GetPixel(logo.Width / 2, logo.Height / 2).A > 0);
    }

    [Fact]
    public void EveryApplicationWindowUsesTheSharedBranding()
    {
        var formTypes = typeof(AppBranding).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(Form).IsAssignableFrom(type)).ToArray();
        Assert.NotEmpty(formTypes);
        Assert.All(formTypes, type => Assert.True(typeof(BrandedForm).IsAssignableFrom(type), type.Name));

        RunOnStaThread(() =>
        {
            var profiles = new[] { new ServerProfile { Name = "Public", ServerAddress = "public" } };
            using var main = new MainForm();
            using var targets = new TargetEditorForm(profiles, null);
            using var networks = new ProfilesForm(profiles);
            using var login = new PublicSignInForm("rustdesk.exe");
            foreach (var form in new Form[] { main, targets, networks, login })
            {
                Assert.Same(AppBranding.Icon, form.Icon);
                Assert.True(form.ShowIcon);
            }
            Assert.Empty(main.Controls.Find("ApplicationLogo", true));
            var picture = Assert.IsType<PictureBox>(Assert.Single(main.Controls.Find("TitleBarIcon", true)));
            Assert.Same(AppBranding.Logo, picture.Image);
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = error; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Form construction timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
