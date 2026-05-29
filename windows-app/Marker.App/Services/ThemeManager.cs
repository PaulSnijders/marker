using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Marker.App.Services;

/// <summary>Swaps the merged theme resource dictionary at runtime.</summary>
public static class ThemeManager
{
    private static ResourceDictionary? _current;

    public static string CurrentTheme { get; private set; } = "light";

    // --- OS title bar -------------------------------------------------

    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    // Layered-window plumbing — used to suppress the first-paint flash of the
    // OS title bar on dialogs (see PrepareDialog).
    private const int GwlExStyle = -20;
    private const int WsExLayered = 0x00080000;
    private const uint LwaAlpha = 0x00000002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte alpha, uint flags);

    /// <summary>
    /// Paints a window's OS title bar to match the current theme. Call once the
    /// window has a handle (e.g. from <c>SourceInitialized</c>).
    /// </summary>
    public static void ApplyTitleBar(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;
        int useDark = CurrentTheme == "dark" ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
    }

    /// <summary>
    /// Themes a modal dialog without the white first-paint flash of the OS
    /// title bar. WPF can't hide the non-client area via <c>Opacity</c>, so
    /// we mark the HWND as a layered window with alpha 0 before it is shown,
    /// flip it to dark mode, and restore alpha once the first paint completes.
    /// </summary>
    public static void PrepareDialog(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            // Hide every pixel (title bar included) until DWM has painted dark.
            int exStyle = GetWindowLong(hwnd, GwlExStyle);
            SetWindowLong(hwnd, GwlExStyle, exStyle | WsExLayered);
            SetLayeredWindowAttributes(hwnd, 0, 0, LwaAlpha);

            ApplyTitleBar(window);
        };

        window.ContentRendered += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;
            SetLayeredWindowAttributes(hwnd, 0, 255, LwaAlpha);
        };
    }

    /// <summary>Applies "light" or "dark"; anything else falls back to light.</summary>
    public static void Apply(string theme)
    {
        string name = string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase)
            ? "Dark" : "Light";
        CurrentTheme = name.ToLowerInvariant();

        var dict = new ResourceDictionary
        {
            Source = new Uri($"Themes/{name}.xaml", UriKind.Relative)
        };

        var merged = Application.Current.Resources.MergedDictionaries;
        if (_current is not null)
            merged.Remove(_current);
        merged.Add(dict);
        _current = dict;
    }

    /// <summary>Toggles between light and dark, returning the new theme name.</summary>
    public static string Toggle()
    {
        string next = CurrentTheme == "dark" ? "light" : "dark";
        Apply(next);
        return next;
    }
}
