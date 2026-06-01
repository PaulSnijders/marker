using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Marker.App.Services;
using Marker.Core.Search;

namespace Marker.App.ViewModels;

/// <summary>
/// State + behaviour for the Replace-in-Files tab. Mirrors
/// <see cref="FindInFilesViewModel"/> for the search half, then layers a
/// replacement string and a commit step on top.
///
/// Replacement preview is computed in the VM (the engine stays pure
/// find-only): each <see cref="SearchMatch"/> from
/// <see cref="WorkspaceSearcher.Search"/> is paired with the text the
/// matched substring would become — literal in plain mode, full
/// <c>Match.Result</c> with $-backrefs in regex mode.
///
/// Editing the replacement box re-renders the previewed items in place
/// without re-walking the file system.
/// </summary>
public sealed partial class ReplaceInFilesViewModel : ObservableObject
{
    public const int MaxMatches = FindInFilesViewModel.MaxMatches;

    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(250);
    private const int FlushEvery = 50;

    private static readonly string[] BuiltInIgnoreDirs =
        { ".git", ".vs", "bin", "obj", "node_modules" };

    private readonly Func<IReadOnlyList<string>> _rootsAccessor;
    private readonly SynchronizationContext? _ui;
    private CancellationTokenSource? _cts;

    // Snapshot of the last completed search, used so that editing only the
    // replacement text can re-render the preview without re-searching.
    private List<SearchMatch> _lastMatches = new();
    private IReadOnlyList<string> _lastRoots = Array.Empty<string>();
    private bool _lastCapped;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _replacementText = "";

    [ObservableProperty] private bool _matchCase;
    [ObservableProperty] private bool _wholeWord;
    [ObservableProperty] private bool _useRegex;

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isSearching;

    /// <summary>Files grouped for the preview tree.</summary>
    public ObservableCollection<FileReplacesViewModel> Files { get; } = new();

    /// <summary>True when there's something to commit and the search hit was complete.</summary>
    public bool CanReplaceAll =>
        !IsSearching && !_lastCapped && Files.Count > 0 && SearchText.Length > 0;

    public ReplaceInFilesViewModel(Func<IReadOnlyList<string>> rootsAccessor)
    {
        _rootsAccessor = rootsAccessor;
        _ui = SynchronizationContext.Current;

        // Share toggle state with Find — one user preference, one place.
        var s = AppServices.Settings;
        _matchCase = s.FindMatchCase;
        _wholeWord = s.FindWholeWord;
        _useRegex = s.FindUseRegex;

        Files.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CanReplaceAll));
    }

    // --- option / text changes ---------------------------------------

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(CanReplaceAll));
        TriggerSearch();
    }

    partial void OnReplacementTextChanged(string value)
    {
        // No new search — just re-render the existing matches with the new
        // replacement so the preview updates as the user types.
        RerenderPreview();
    }

    partial void OnMatchCaseChanged(bool value)
    {
        AppServices.Settings.FindMatchCase = value;
        AppServices.SaveSettings();
        TriggerSearch();
    }

    partial void OnWholeWordChanged(bool value)
    {
        AppServices.Settings.FindWholeWord = value;
        AppServices.SaveSettings();
        TriggerSearch();
    }

    partial void OnUseRegexChanged(bool value)
    {
        AppServices.Settings.FindUseRegex = value;
        AppServices.SaveSettings();
        TriggerSearch();
    }

    partial void OnIsSearchingChanged(bool value) => OnPropertyChanged(nameof(CanReplaceAll));

    // --- search engine ----------------------------------------------

    private void TriggerSearch()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        if (string.IsNullOrEmpty(SearchText))
        {
            PostToUi(() =>
            {
                Files.Clear();
                _lastMatches.Clear();
                _lastCapped = false;
                StatusText = "";
                IsSearching = false;
                OnPropertyChanged(nameof(CanReplaceAll));
            });
            return;
        }

        _ = RunSearchAsync(ct);
    }

    private async Task RunSearchAsync(CancellationToken ct)
    {
        try { await Task.Delay(DebounceDelay, ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        var roots = _rootsAccessor();
        var opts = new SearchOptions(SearchText, MatchCase, WholeWord, UseRegex);
        var ignore = BuildIgnoreSet();

        PostToUi(() =>
        {
            Files.Clear();
            StatusText = "Searching…";
            IsSearching = true;
        });

        var collected = new List<SearchMatch>();

        try
        {
            await foreach (var m in WorkspaceSearcher.Search(roots, opts, ignore, MaxMatches, ct)
                                                     .ConfigureAwait(false))
            {
                collected.Add(m);
            }
        }
        catch (OperationCanceledException) { return; }

        if (ct.IsCancellationRequested)
            return;

        bool capped = collected.Count >= MaxMatches;
        _lastMatches = collected;
        _lastRoots = roots;
        _lastCapped = capped;

        Regex? regex = TryBuildRegex(opts);
        PostToUi(() =>
        {
            Files.Clear();
            if (regex is not null)
                BuildPreview(regex);
            UpdateStatus(capped);
            IsSearching = false;
            OnPropertyChanged(nameof(CanReplaceAll));
        });
    }

    /// <summary>Rebuilds the preview tree from the cached matches + current replacement.</summary>
    private void RerenderPreview()
    {
        if (_lastMatches.Count == 0)
            return;
        var opts = new SearchOptions(SearchText, MatchCase, WholeWord, UseRegex);
        var regex = TryBuildRegex(opts);
        if (regex is null)
            return;

        PostToUi(() =>
        {
            Files.Clear();
            BuildPreview(regex);
            UpdateStatus(_lastCapped);
            OnPropertyChanged(nameof(CanReplaceAll));
        });
    }

    /// <summary>
    /// Groups the cached matches per file, pairing each with the per-match
    /// replacement text. Uses the regex's <c>Match.Result</c> in regex mode
    /// so <c>$1</c> / <c>${name}</c> backrefs resolve correctly.
    /// </summary>
    private void BuildPreview(Regex regex)
    {
        var grouped = new Dictionary<string, FileReplacesViewModel>(StringComparer.OrdinalIgnoreCase);
        var pending = new List<FileReplacesViewModel>();

        foreach (var m in _lastMatches)
        {
            string replacement = ResolveReplacement(regex, m);
            var item = new ReplacePreviewItem(m, replacement);

            if (!grouped.TryGetValue(m.FilePath, out var group))
            {
                group = new FileReplacesViewModel(m.FilePath, MakeRelative(_lastRoots, m.FilePath));
                grouped[m.FilePath] = group;
                pending.Add(group);
            }
            group.Items.Add(item);
        }

        foreach (var g in pending)
            Files.Add(g);
    }

    private string ResolveReplacement(Regex regex, SearchMatch sm)
    {
        if (!UseRegex)
            return ReplacementText; // literal mode — no $ substitution

        // Re-find the match at its known position so we get a Match object
        // with the actual capture groups, then evaluate the user's pattern.
        var m = regex.Match(sm.LineText, sm.ColumnStart);
        if (!m.Success || m.Index != sm.ColumnStart)
            return ReplacementText;
        try { return m.Result(ReplacementText); }
        catch (ArgumentException) { return ReplacementText; } // invalid backref
    }

    private void UpdateStatus(bool capped)
    {
        int matches = _lastMatches.Count;
        int files = Files.Count;
        StatusText = capped
            ? $"Stopped at {MaxMatches:N0} matches — refine your search before replacing."
            : matches == 0
                ? "No matches."
                : $"{matches:N0} match{(matches == 1 ? "" : "es")} in {files:N0} file{(files == 1 ? "" : "s")}.";
    }

    // --- commit -------------------------------------------------------

    /// <summary>
    /// Replaces every match in every previewed file by reading the file,
    /// running <see cref="Regex.Replace(string, string)"/> across the whole
    /// content (so multi-match lines are handled in one pass), then writing
    /// it back via <see cref="AppServices.Files"/>. Returns the list of
    /// successfully-written paths so the host can reload any open tabs.
    /// </summary>
    public async Task<(int FilesWritten, int FilesFailed, IReadOnlyList<string> WrittenPaths)>
        CommitReplaceAllAsync()
    {
        if (!CanReplaceAll)
            return (0, 0, Array.Empty<string>());

        var opts = new SearchOptions(SearchText, MatchCase, WholeWord, UseRegex);
        var regex = TryBuildRegex(opts);
        if (regex is null)
            return (0, 0, Array.Empty<string>());

        var files = Files.Select(f => f.FilePath).ToList();
        string replacement = ReplacementText;

        PostToUi(() =>
        {
            StatusText = "Replacing…";
            IsSearching = true;
            OnPropertyChanged(nameof(CanReplaceAll));
        });

        int written = 0;
        int failed = 0;
        var ok = new List<string>(files.Count);

        await Task.Run(() =>
        {
            foreach (string path in files)
            {
                try
                {
                    var content = AppServices.Files.ReadText(path);
                    if (content.IsBinary)
                        continue;
                    string updated = regex.Replace(content.Text, replacement);
                    if (updated == content.Text)
                        continue;
                    // TextFileContent properties are init-only — copy metadata,
                    // swap the text, keep encoding/BOM/line-ending intact.
                    var rewritten = new Core.FileSystem.TextFileContent
                    {
                        Text = updated,
                        Encoding = content.Encoding,
                        HasBom = content.HasBom,
                        LineEnding = content.LineEnding,
                        IsBinary = false,
                    };
                    AppServices.Files.WriteText(path, rewritten);
                    written++;
                    ok.Add(path);
                }
                catch
                {
                    failed++;
                }
            }
        }).ConfigureAwait(false);

        PostToUi(() =>
        {
            Files.Clear();
            _lastMatches.Clear();
            _lastCapped = false;
            IsSearching = false;
            StatusText = failed == 0
                ? $"Replaced in {written:N0} file{(written == 1 ? "" : "s")}."
                : $"Replaced in {written:N0} file{(written == 1 ? "" : "s")} — {failed:N0} failed.";
            OnPropertyChanged(nameof(CanReplaceAll));
        });

        return (written, failed, ok);
    }

    // --- helpers ------------------------------------------------------

    /// <summary>Same compile path as the engine — kept here so the VM can resolve <c>$</c> backrefs.</summary>
    private static Regex? TryBuildRegex(SearchOptions opts)
    {
        if (string.IsNullOrEmpty(opts.Pattern))
            return null;
        try
        {
            string body = opts.UseRegex ? opts.Pattern : Regex.Escape(opts.Pattern);
            if (opts.WholeWord)
                body = $@"\b(?:{body})\b";
            var flags = RegexOptions.Compiled | RegexOptions.CultureInvariant;
            if (!opts.MatchCase)
                flags |= RegexOptions.IgnoreCase;
            return new Regex(body, flags, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

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
