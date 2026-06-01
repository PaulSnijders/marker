using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Marker.Core.Search;

namespace Marker.App.Views;

/// <summary>
/// Attached property that paints a <see cref="SearchMatch"/> into a
/// <see cref="TextBlock"/> as three runs (prefix, matched, suffix) so the
/// matched substring stands out from its surrounding line.
///
/// Long lines (e.g. minified JS) are windowed around the match so the UI
/// stays readable and rows stay narrow.
/// </summary>
public static class MatchHighlight
{
    /// <summary>Max characters shown either side of the match.</summary>
    private const int Window = 200;

    public static readonly DependencyProperty MatchProperty =
        DependencyProperty.RegisterAttached(
            "Match", typeof(SearchMatch), typeof(MatchHighlight),
            new PropertyMetadata(null, OnMatchChanged));

    public static SearchMatch? GetMatch(DependencyObject d) =>
        (SearchMatch?)d.GetValue(MatchProperty);

    public static void SetMatch(DependencyObject d, SearchMatch? value) =>
        d.SetValue(MatchProperty, value);

    private static void OnMatchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb)
            return;

        tb.Inlines.Clear();

        if (e.NewValue is not SearchMatch m)
            return;

        string line = m.LineText ?? "";
        int start = Math.Clamp(m.ColumnStart, 0, line.Length);
        int end = Math.Clamp(start + m.Length, start, line.Length);

        // Window the line around the match so a 100 KB minified line is bearable.
        int prefixStart = Math.Max(0, start - Window);
        int suffixEnd = Math.Min(line.Length, end + Window);
        string ell = "…";

        string prefix = line.Substring(prefixStart, start - prefixStart);
        string matched = line.Substring(start, end - start);
        string suffix = line.Substring(end, suffixEnd - end);

        // Drop leading indentation noise so matches don't drift off-screen.
        prefix = prefix.TrimStart();

        if (prefixStart > 0)
            tb.Inlines.Add(new Run(ell));
        tb.Inlines.Add(new Run(prefix));

        // Accent is the same blue in light + dark themes, so white text on it
        // works for both. (Don't bind Foreground to a themed brush — in dark
        // mode AppBackground is near-black, which gave us black-on-blue.)
        var hit = new Run(matched)
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = System.Windows.Media.Brushes.White
        };
        hit.SetResourceReference(TextElement.BackgroundProperty, "AccentBrush");
        tb.Inlines.Add(hit);

        tb.Inlines.Add(new Run(suffix));
        if (suffixEnd < line.Length)
            tb.Inlines.Add(new Run(ell));
    }
}
