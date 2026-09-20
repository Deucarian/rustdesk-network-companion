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
    public void WindowsIconFramesFillTheirCanvasWithBalancedPadding()
    {
        using var stream = typeof(AppBranding).Assembly.GetManifestResourceStream(AppBranding.IconResourceName)!;
        using var reader = new BinaryReader(stream);
        reader.ReadUInt16(); reader.ReadUInt16();
        var count = reader.ReadUInt16();
        var frames = new List<(int Size, int Length, int Offset)>();
        for (var index = 0; index < count; index++)
        {
            var size = reader.ReadByte();
            reader.ReadBytes(7);
            frames.Add((size == 0 ? 256 : size, reader.ReadInt32(), reader.ReadInt32()));
        }
        foreach (var frame in frames)
        {
            stream.Position = frame.Offset;
            using var png = new MemoryStream(reader.ReadBytes(frame.Length));
            using var image = new Bitmap(png);
            var left = image.Width; var top = image.Height; var right = -1; var bottom = -1;
            for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
            {
                if (image.GetPixel(x, y).A < 128) continue;
                left = Math.Min(left, x); top = Math.Min(top, y);
                right = Math.Max(right, x); bottom = Math.Max(bottom, y);
            }
            Assert.Equal(frame.Size, image.Width);
            Assert.Equal(frame.Size, image.Height);
            Assert.True(right - left + 1 >= frame.Size * .85, $"The {frame.Size}px frame has excessive horizontal padding.");
            Assert.True(bottom - top + 1 >= frame.Size * .90, $"The {frame.Size}px frame has excessive vertical padding.");
            Assert.InRange(Math.Abs(left - (image.Width - 1 - right)), 0, 1);
            Assert.InRange(Math.Abs(top - (image.Height - 1 - bottom)), 0, 1);
            Assert.Equal(0, image.GetPixel(0, 0).A);
        }
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
