namespace Marker.Core.Search;

/// <summary>
/// What the user typed plus the toggle state of the three find options.
/// </summary>
/// <param name="Pattern">Raw search string from the find box.</param>
/// <param name="MatchCase">When false, the search is case-insensitive.</param>
/// <param name="WholeWord">When true, the pattern is wrapped in <c>\b…\b</c>.</param>
/// <param name="UseRegex">When false, <see cref="Pattern"/> is treated as a literal.</param>
public sealed record SearchOptions(
    string Pattern,
    bool MatchCase,
    bool WholeWord,
    bool UseRegex);
