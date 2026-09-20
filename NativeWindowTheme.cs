using System.Runtime.InteropServices;

namespace Simultria.RustDeskCompanion;

// Presentation only: never replace the native frame or intercept window commands.
internal static class NativeWindowTheme
{
    private const int DefaultColor = -1;
    private const int UseImmersiveDarkMode = 20, CornerPreference = 33;
    private const int BorderColor = 34, CaptionColor = 35, TextColor = 36;

    internal static bool Apply(IntPtr window, bool active)
    {
        // Earlier Windows versions retain their complete, standard system frame.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return false;
        var highContrast = SystemInformation.HighContrast;
        var applied = Set(window, UseImmersiveDarkMode, 0);
        applied &= Set(window, CornerPreference, highContrast ? 0 : 2);
        applied &= Set(window, CaptionColor, highContrast ? DefaultColor : ColorTranslator.ToWin32(AppTheme.Canvas));
        applied &= Set(window, TextColor, highContrast ? DefaultColor : ColorTranslator.ToWin32(active ? AppTheme.Ink : AppTheme.Muted));
        applied &= Set(window, BorderColor, highContrast ? DefaultColor : ColorTranslator.ToWin32(active ? AppTheme.WindowBorder : AppTheme.Line));
        return applied;
    }

    private static bool Set(IntPtr window, int attribute, int value)
    {
        // Styling is optional; if DWM rejects an attribute, native behaviour remains.
        return DwmSetWindowAttribute(window, attribute, ref value, sizeof(int)) >= 0;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
