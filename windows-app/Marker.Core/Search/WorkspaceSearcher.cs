using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Marker.Core.FileSystem;

namespace Marker.Core.Search;

/// <summary>
/// Walks a set of workspace roots and yields each match of the supplied
/// pattern, lazily. Designed for the Find-in-Files panel: an
/// <see cref="IAsyncEnumerable{T}"/> so the UI can stream results as they
/// arrive and cancel a stale query mid-flight when the user keeps typing.
///
/// The walk is plain iterative DFS (a stack), not <c>Directory.EnumerateFiles
/// (..., AllDirectories)</c>, so we can honour the per-directory ignore set
/// and ride through unreadable folders without aborting the whole search.
/// Binaries are skipped via the shared <see cref="BinarySniff"/>.
/// </summary>
public static class WorkspaceSearcher
{
    /// <summary>
    /// Yields up to <paramref name="maxMatches"/> matches across the
    /// workspace. Stops eagerly when the cap is hit or the token is
    /// cancelled. Returns nothing for an empty/whitespace pattern.
    /// </summary>
    /// <param name="roots">Absolute paths to workspace root folders.</param>
    /// <param name="opts">Pattern + the three option toggles.</param>
    /// <param name="ignoreDirs">Directory names to skip (e.g. <c>.git</c>,
    /// <c>node_modules</c>). Case-insensitive.</param>
    /// <param name="maxMatches">Hard cap so a single-letter search can't
    /// blow up memory. The caller decides what to show when reached.</param>
    public static async IAsyncEnumerable<SearchMatch> Search(
        IReadOnlyList<string> roots,
        SearchOptions opts,
        IReadOnlySet<string> ignoreDirs,
        int maxMatches,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(opts.Pattern) || maxMatches <= 0)
            yield break;

        Regex regex;
        try
        {
            regex = BuildRegex(opts);
        }
        catch (ArgumentException)
        {
            // Invalid regex while the user is typing — treat as "no matches".
            yield break;
        }

        int total = 0;
        foreach (string root in roots)
        {
            if (ct.IsCancellationRequested || total >= maxMatches)
                yield break;
            if (!Directory.Exists(root))
                continue;

            await foreach (var match in SearchRoot(root, regex, ignoreDirs, maxMatches, t => total = t, () => total, ct))
            {
                yield return match;
                if (total >= maxMatches)
                    yield break;
            }
        }
    }

    private static async IAsyncEnumerable<SearchMatch> SearchRoot(
        string root,
        Regex regex,
        IReadOnlySet<string> ignoreDirs,
        int maxMatches,
        Action<int> setTotal,
        Func<int> getTotal,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            if (ct.IsCancellationRequested || getTotal() >= maxMatches)
                yield break;

            string dir = pending.Pop();

            // Push subdirectories first so the order doesn't matter for files.
            try
            {
                foreach (string sub in Directory.EnumerateDirectories(dir))
                {
                    if (!ignoreDirs.Contains(Path.GetFileName(sub)))
                        pending.Push(sub);
                }
            }
            catch { /* unreadable folder — its files are simply absent */ }

            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch { continue; }

            foreach (string file in files)
            {
                if (ct.IsCancellationRequested || getTotal() >= maxMatches)
                    yield break;
                if (BinarySniff.LooksBinary(file))
                    continue;

                await foreach (var match in SearchFile(file, regex, maxMatches - getTotal(), ct))
                {
                    setTotal(getTotal() + 1);
                    yield return match;
                    if (getTotal() >= maxMatches)
                        yield break;
                }
            }
        }
    }

    /// <summary>
    /// Reads one file line-by-line and yields every regex match. Uses
    /// <see cref="StreamReader"/> so a huge file streams instead of loading
    /// fully into memory. UTF-8 by default with BOM auto-detection; that
    /// matches what users expect on Windows for source/notes/markdown.
    /// </summary>
    private static async IAsyncEnumerable<SearchMatch> SearchFile(
        string path,
        Regex regex,
        int budget,
        [EnumeratorCancellation] CancellationToken ct)
    {
        FileStream? stream = null;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
        }
        catch
        {
            yield break;
        }

        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        int lineNumber = 0;
        int remaining = budget;

        while (true)
        {
            if (ct.IsCancellationRequested || remaining <= 0)
                yield break;

            string? line;
            try
            {
                line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch
            {
                // A read error (e.g. bad encoding partway through) ends the
                // file — we'd rather skip it than fail the whole search.
                yield break;
            }
            if (line is null)
                yield break;

            lineNumber++;
            foreach (Match m in regex.Matches(line))
            {
                if (m.Length == 0)
                    continue; // skip zero-width matches (e.g. `^`, `\b`)
                yield return new SearchMatch(path, lineNumber, m.Index, m.Length, line);
                remaining--;
                if (remaining <= 0)
                    yield break;
            }
        }
    }

    /// <summary>Compiles the user's pattern + flags to a single <see cref="Regex"/>.</summary>
    private static Regex BuildRegex(SearchOptions opts)
    {
        string body = opts.UseRegex ? opts.Pattern : Regex.Escape(opts.Pattern);
        if (opts.WholeWord)
            body = $@"\b(?:{body})\b";

        var flags = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        if (!opts.MatchCase)
            flags |= RegexOptions.IgnoreCase;

        return new Regex(body, flags, TimeSpan.FromSeconds(1));
    }
}
