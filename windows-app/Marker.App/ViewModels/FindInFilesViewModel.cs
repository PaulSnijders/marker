using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Marker.App.Services;
using Marker.Core.Search;

namespace Marker.App.ViewModels;

/// <summary>
/// UI state for the Find-in-Files panel: the query, the three option toggles,
/// the grouped results and a short status line. Any property change triggers
/// a fresh search after a short debounce; the previous search is cancelled.
///
/// The VM owns the dispatch back to the UI thread: it captures the
/// <see cref="SynchronizationContext"/> of whichever thread created it (the
/// UI thread, in practice) and posts result-list mutations there so callers
/// don't have to think about threading.
/// </summary>
public sealed partial class FindInFilesViewModel : ObservableObject
{
    /// <summary>Hard cap on results across all files. See the plan/decisions.</summary>
    public const int MaxMatches = 1000;

    /// <summary>How long to wait after the last keystroke before searching.</summary>
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>Batch size for posting new matches to the UI.</summary>
    private const int FlushEvery = 50;

    /// <summary>
    /// Folder names that are never searched. Matches the set used by
    /// quick-open + the file tree, plus whatever the user added under
    /// <see cref="Core.Models.AppSettings.IgnorePatterns"/>.
    /// </summary>
    private static readonly string[] BuiltInIgnoreDirs =
        { ".git", ".vs", "bin", "obj", "node_modules" };

    private readonly Func<IReadOnlyList<string>> _rootsAccessor;
    private readonly SynchronizationContext? _ui;
    private CancellationTokenSource? _cts;

    /// <summary>The current text in the find box.</summary>
    [ObservableProperty] private string _searchText = "";

    [ObservableProperty] private bool _matchCase;
    [ObservableProperty] private bool _wholeWord;
    [ObservableProperty] private bool _useRegex;

    /// <summary>"Searching…", "12 matches in 3 files", "Stopped at 1,000 matches — refine your search."</summary>
    [ObservableProperty] private string _statusText = "";

    /// <summary>True while a search is running.</summary>
    [ObservableProperty] private bool _isSearching;

    /// <summary>Results grouped by file. UI binds to this.</summary>
    public ObservableCollection<FileMatchesViewModel> Files { get; } = new();

    /// <summary>True when a search has produced no matches (for an empty-state hint).</summary>
    public bool HasNoResults => Files.Count == 0 && !IsSearching && SearchText.Length > 0;

    public FindInFilesViewModel(Func<IReadOnlyList<string>> rootsAccessor)
    {
        _rootsAccessor = rootsAccessor;
        _ui = SynchronizationContext.Current;

        // Restore toggle state from settings. Done in-constructor so the
        // generated OnXChanged hooks below don't write through during restore.
        var s = AppServices.Settings;
        _matchCase = s.FindMatchCase;
        _wholeWord = s.FindWholeWord;
        _useRegex = s.FindUseRegex;

        Files.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoResults));
    }

    // --- option toggles: persist and re-run --------------------------

    partial void OnSearchTextChanged(string value) => Trigger();

    partial void OnMatchCaseChanged(bool value)
    {
        AppServices.Settings.FindMatchCase = value;
        AppServices.SaveSettings();
        Trigger();
    }

    partial void OnWholeWordChanged(bool value)
    {
        AppServices.Settings.FindWholeWord = value;
        AppServices.SaveSettings();
        Trigger();
    }

    partial void OnUseRegexChanged(bool value)
    {
        AppServices.Settings.FindUseRegex = value;
        AppServices.SaveSettings();
        Trigger();
    }

    partial void OnIsSearchingChanged(bool value) => OnPropertyChanged(nameof(HasNoResults));

    // --- engine --------------------------------------------------------

    /// <summary>
    /// Cancel the in-flight search (if any) and queue a new one after the
    /// debounce delay. Empty query just clears the results immediately.
    /// </summary>
    private void Trigger()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        if (string.IsNullOrEmpty(SearchText))
        {
            PostToUi(() =>
            {
                Files.Clear();
                StatusText = "";
                IsSearching = false;
            });
            return;
        }

        // Fire and forget; cancellation handles staleness.
        _ = RunSearchAsync(ct);
    }

    private async Task RunSearchAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(DebounceDelay, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return; }

        // Snapshot the inputs once so toggling them mid-search can't corrupt the run.
        var roots = _rootsAccessor();
        var opts = new SearchOptions(SearchText, MatchCase, WholeWord, UseRegex);
        var ignore = BuildIgnoreSet();

        PostToUi(() =>
        {
            Files.Clear();
            StatusText = "Searching…";
            IsSearching = true;
        });

        var grouped = new Dictionary<string, FileMatchesViewModel>(StringComparer.OrdinalIgnoreCase);
        int matchCount = 0;
        int fileCount = 0;
        bool capped = false;
        var pending = new List<(FileMatchesViewModel group, SearchMatch match, bool isNewGroup)>(FlushEvery);

        try
        {
            await foreach (var m in WorkspaceSearcher.Search(roots, opts, ignore, MaxMatches, ct)
                                                     .ConfigureAwait(false))
            {
                bool isNewGroup = !grouped.TryGetValue(m.FilePath, out var group);
                if (group is null)
                {
                    string relative = MakeRelative(roots, m.FilePath);
                    group = new FileMatchesViewModel(m.FilePath, relative);
                    grouped[m.FilePath] = group;
                    fileCount++;
                }

                pending.Add((group, m, isNewGroup));
                matchCount++;

                if (pending.Count >= FlushEvery)
                    Flush(pending);
            }
            capped = matchCount >= MaxMatches;
        }
        catch (OperationCanceledException) { return; }

        if (ct.IsCancellationRequested)
            return;

        if (pending.Count > 0)
            Flush(pending);

        // Capture for the closure (member access from a lambda needs locals).
        int finalMatches = matchCount;
        int finalFiles = fileCount;
        bool finalCapped = capped;
        PostToUi(() =>
        {
            IsSearching = false;
            StatusText = finalCapped
                ? $"Stopped at {MaxMatches:N0} matches — refine your search."
                : finalMatches == 0
                    ? "No matches."
                    : $"{finalMatches:N0} match{(finalMatches == 1 ? "" : "es")} in {finalFiles:N0} file{(finalFiles == 1 ? "" : "s")}.";
        });
    }

    /// <summary>Posts the accumulated batch to the UI thread and clears it.</summary>
    private void Flush(List<(FileMatchesViewModel group, SearchMatch match, bool isNewGroup)> pending)
    {
        // Copy because we're about to clear the list and post asynchronously.
        var batch = pending.ToArray();
        pending.Clear();
        PostToUi(() =>
        {
            foreach (var (group, match, isNewGroup) in batch)
            {
                if (isNewGroup)
                    Files.Add(group);
                group.Matches.Add(match);
            }
        });
    }

    /// <summary>
    /// Combined ignore set: built-ins (matching the file tree) ∪ user
    /// patterns from settings. Patterns that look like globs are dropped —
    /// only bare directory names are honoured here.
    /// </summary>
    private static HashSet<string> BuildIgnoreSet()
    {
        var set = new HashSet<string>(BuiltInIgnoreDirs, StringComparer.OrdinalIgnoreCase);
        foreach (string p in AppServices.Settings.IgnorePatterns)
        {
            if (!string.IsNullOrWhiteSpace(p) &&
                p.IndexOfAny(new[] { '*', '?', '/', '\\' }) < 0)
            {
                set.Add(p);
            }
        }
        return set;
    }

    private static string MakeRelative(IReadOnlyList<string> roots, string filePath)
    {
        foreach (string root in roots)
        {
            if (filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return Path.GetRelativePath(root, filePath);
        }
        return filePath;
    }

    private void PostToUi(Action action)
    {
        if (_ui is null) { action(); return; }
        _ui.Post(_ => action(), null);
    }
}
