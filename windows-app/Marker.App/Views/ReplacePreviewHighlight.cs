using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Marker.App.ViewModels;

namespace Marker.App.Views;

/// <summary>
/// Renders a <see cref="ReplacePreviewItem"/> into a single
/// <see cref="TextBlock"/> as two stacked lines: the original line with the
/// matched substring struck-through, and below it the same line with the
/// replacement painted in the accent colour. Long lines are windowed around
/// the match so a 100 KB minified line is bearable.
///
/// Stays a single TextBlock (not a StackPanel) so it virtualises cleanly
/// inside the TreeView.
/// </summary>
public static class ReplacePreviewHighlight
{
    /// <summary>Max characters shown either side of the match.</summary>
    private const int Window = 200;

    public static readonly DependencyProperty ItemProperty =
        DependencyProperty.RegisterAttached(
            "Item", typeof(ReplacePreviewItem), typeof(ReplacePreviewHighlight),
            new PropertyMetadata(null, OnItemChanged));

    public static ReplacePreviewItem? GetItem(DependencyObject d) =>
        (ReplacePreviewItem?)d.GetValue(ItemProperty);

    public static void SetItem(DependencyObject d, ReplacePreviewItem? value) =>
        d.SetValue(ItemProperty, value);

    private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb)
            return;

        tb.Inlines.Clear();

        if (e.NewValue is not ReplacePreviewItem item)
            return;

        string line = item.LineText ?? "";
        int start = Math.Clamp(item.ColumnStart, 0, line.Length);
        int end = Math.Clamp(start + item.Length, start, line.Length);

        int prefixStart = Math.Max(0, start - Window);
        int suffixEnd = Math.Min(line.Length, end + Window);
        bool leadEll = prefixStart > 0;
        bool trailEll = suffixEnd < line.Length;

        string prefix = line.Substring(prefixStart, start - prefixStart).TrimStart();
        string oldText = line.Substring(start, end - start);
        string suffix = line.Substring(end, suffixEnd - end);
        string newText = item.Replacement ?? "";

        // --- old (top) line: prefix [struck-through old] suffix
        if (leadEll) tb.Inlines.Add(MutedRun("…"));
        if (prefix.Length > 0) tb.Inlines.Add(new Run(prefix));
        var removed = new Run(oldText) { TextDecorations = TextDecorations.Strikethrough };
        tb.Inlines.Add(removed);
        if (suffix.Length > 0) tb.Inlines.Add(new Run(suffix));
        if (trailEll) tb.Inlines.Add(MutedRun("…"));

        tb.Inlines.Add(new LineBreak());

        // --- new (bottom) line: prefix [accent new] suffix
        if (leadEll) tb.Inlines.Add(MutedRun("…"));
        if (prefix.Length > 0) tb.Inlines.Add(new Run(prefix));

        // Show an explicit "·" for an empty replacement (= "delete the match")
        // so the user sees something landed there.
        var added = new Run(newText.Length == 0 ? "·" : newText)
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
        };
        added.SetResourceReference(TextElement.BackgroundProperty, "AccentBrush");
        tb.Inlines.Add(added);

        if (suffix.Length > 0) tb.Inlines.Add(new Run(suffix));
        if (trailEll) tb.Inlines.Add(MutedRun("…"));
    }

    private static Run MutedRun(string text)
    {
        var run = new Run(text);
        run.SetResourceReference(TextElement.ForegroundProperty, "MutedForeground");
        return run;
    }
}
