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
