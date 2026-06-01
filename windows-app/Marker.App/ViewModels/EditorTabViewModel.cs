using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using ICSharpCode.AvalonEdit.Document;
using Marker.Core.FileSystem;
using Marker.Core.Models;

namespace Marker.App.ViewModels;

/// <summary>
/// One open file in the editor. Owns its own <see cref="TextDocument"/> so each
/// tab keeps an independent undo stack while sharing a single editor control.
/// </summary>
public sealed partial class EditorTabViewModel : ObservableObject
{
    private TextFileContent _origin;

    [ObservableProperty] private string _filePath;
    [ObservableProperty] private string _title;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private MarkdownMode _mode;

    /// <summary>
    /// Disk LastWriteTime (UTC) the last time we read or wrote this file —
    /// used to spot external edits when the window is reactivated.
    /// </summary>
    public DateTime DiskTimestampUtc { get; set; }

    /// <summary>
    /// Disk size at the same moment as <see cref="DiskTimestampUtc"/> — a
    /// second axis so quick saves with a 1-second-resolution clock still
    /// register as a change.
    /// </summary>
    public long DiskSize { get; set; }

    /// <summary>
    /// True for a transient "sneak-peek" tab opened with the right-arrow in the
    /// file tree. Such a tab is shown in italic, is not persisted with the
    /// workspace, and is replaced as the tree selection moves.
    /// </summary>
    [ObservableProperty] private bool _isPreview;
    [ObservableProperty] private int _caretLine = 1;
    [ObservableProperty] private int _caretColumn = 1;

    /// <summary>The editable text + undo history for this file.</summary>
    public TextDocument Document { get; }

    public FileTypeInfo FileType { get; }

    /// <summary>True when the file is binary and cannot be edited.</summary>
    public bool IsBinary { get; }

    /// <summary>True when markdown view-mode switching applies to this tab.</summary>
    public bool IsMarkdown => FileType.IsMarkdown && !IsBinary;

    public string EncodingLabel => _origin.EncodingLabel;
    public string LineEndingLabel => _origin.LineEndingLabel;
    public string TypeName => IsBinary ? "Binary" : FileType.DisplayName;

    public EditorTabViewModel(string path, TextFileContent content, FileTypeInfo type, MarkdownMode initialMode)
    {
        _filePath = path;
        _title = Path.GetFileName(path);
        _origin = content;
        FileType = type;
        IsBinary = content.IsBinary;
        _mode = type.IsMarkdown ? initialMode : MarkdownMode.Source;

        Document = new TextDocument(content.IsBinary ? string.Empty : content.Text);
        // Subscribe after construction so the initial load is not "dirty".
        Document.TextChanged += (_, _) => IsDirty = true;
    }

    /// <summary>Packages the current text with the file's original encoding for saving.</summary>
    public TextFileContent ToContent() => new()
    {
        Text = Document.Text,
        Encoding = _origin.Encoding,
        HasBom = _origin.HasBom,
        LineEnding = _origin.LineEnding,
        IsBinary = false
    };

    /// <summary>Updates the path and title after a rename on disk.</summary>
    public void UpdatePath(string newPath)
    {
        FilePath = newPath;
        Title = Path.GetFileName(newPath);
    }

    /// <summary>
    /// Replaces the document contents with a freshly-read disk snapshot. The
    /// origin (encoding, line endings) is swapped too so the next save uses
    /// whatever the file looks like now. <see cref="IsDirty"/> is cleared
    /// after the TextChanged handler has flipped it true.
    /// </summary>
    public void ReloadFrom(TextFileContent fresh)
    {
        _origin = fresh;
        Document.Text = fresh.Text;
        IsDirty = false;
    }
}
