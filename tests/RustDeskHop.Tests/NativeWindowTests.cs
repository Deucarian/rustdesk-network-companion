using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Simultria.RustDeskCompanion;
using Xunit;

namespace RustDeskHop.Tests;

public sealed class NativeWindowTests
{
    private const int Caption = 0x00C00000, SystemMenu = 0x00080000;
    private const int ResizeFrame = 0x00040000, Minimize = 0x00020000, Maximize = 0x00010000;

    [Fact]
    public void AllWindowsUseRealCaptionsAndNativeWindowCapabilities() => OnSta(() =>
    {
        var profiles = new[] { new ServerProfile { Name = "Public", ServerAddress = "public" } };
        using var main = new MainForm(new AppSettings { Profiles = profiles.ToList() });
        using var networks = new ProfilesForm(profiles);
        using var computer = new TargetEditorForm(profiles, null);
        using var signIn = new PublicSignInForm("not-launched-during-this-test.exe");
        // Do not show signIn: its Shown event launches RustDesk.
        foreach (var form in new Form[] { main, networks, computer, signIn })
        {
            var style = GetWindowLong(form.Handle, -16);
            Assert.Equal(Caption | SystemMenu, style & (Caption | SystemMenu));
            Assert.Equal(form.MinimizeBox, (style & Minimize) != 0);
            Assert.Equal(form.MaximizeBox, (style & Maximize) != 0);
            Assert.Equal(form.FormBorderStyle == FormBorderStyle.Sizable, (style & ResizeFrame) != 0);
            Assert.Same(AppBranding.Icon, form.Icon);
            Assert.True(form.Height - form.ClientSize.Height >= SystemInformation.CaptionHeight);
            var content = Assert.IsType<Panel>(Assert.Single(form.Controls.Cast<Control>()));
            Assert.Equal("WindowContent", content.Name);
            Assert.Equal(form.ClientRectangle, content.Bounds);
            Assert.Equal(Padding.Empty, form.Padding);
        }
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DwmAcceptsNativeCaptionThemeForBothActivationStates(bool active) => OnSta(() =>
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        using var form = new MainForm(new AppSettings());
        // Caption/text colours are set-only DWM attributes, not queryable attributes.
        Assert.True(NativeWindowTheme.Apply(form.Handle, active));
    });

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Native shell test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

}
