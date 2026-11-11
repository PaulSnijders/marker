using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Marker.Core.FileTypes;

namespace Marker.App.Views;

/// <summary>
/// Editable grid over CSV/TSV text. The tab's text document stays the source
/// of truth: <see cref="Load"/> parses it, edits only mark the grid dirty, and
/// <see cref="TakeText"/> serializes back when the host flushes (save, mode or
/// tab switch). Every row is data — the first row is just shown bold.
/// </summary>
public partial class CsvGridView : UserControl
{
    private const string CountColumn = "__n";   // original field count per row

    private DataTable? _table;
    private char _separator = ',';
    private string _newline = "\r\n";
    private bool _trailingNewline = true;
    private bool _loading;

    /// <summary>True when the grid holds edits not yet written back to text.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>The separator the current grid was parsed with.</summary>
    public char Separator => _separator;

    /// <summary>Raised on the first edit after a load/flush, and on every edit after.</summary>
    public event EventHandler? Edited;

    /// <summary>
    /// The user picked another separator: <c>null</c> = auto-detect. The host
    /// flushes pending edits with the old one, then reloads with the new one.
    /// </summary>
    public event EventHandler<char?>? SeparatorChosen;

    public CsvGridView()
    {
        InitializeComponent();
    }

    /// <summary>Parses <paramref name="text"/> into the grid.</summary>
    /// <param name="chosen">User-picked separator, or <c>null</c> to auto-detect.</param>
    /// <param name="fallback">Separator used when detection finds nothing (tab for .tsv).</param>
    public void Load(string text, char? chosen, char fallback)
    {
        _loading = true;
        try
        {
            char detected = Csv.DetectSeparator(text, fallback);
            _separator = chosen ?? detected;
            _newline = text.Contains("\r\n") ? "\r\n" : "\n";
            _trailingNewline = text.Length == 0 || text.EndsWith('\n');

            var rows = Csv.Parse(text, _separator).ToList();
            int columns = Math.Max(1, rows.Count == 0 ? 1 : rows.Max(r => r.Length));

            var table = new DataTable();
            for (int c = 0; c < columns; c++)
                table.Columns.Add("c" + c, typeof(string)).DefaultValue = "";
            table.Columns.Add(CountColumn, typeof(int)).DefaultValue = columns;

            table.BeginLoadData();
            foreach (var r in rows)
            {
                var values = new object[columns + 1];
                for (int c = 0; c < columns; c++)
                    values[c] = c < r.Length ? r[c] : "";
                values[columns] = r.Length;
                table.Rows.Add(values);
            }
            table.EndLoadData();
            table.AcceptChanges();

            BuildColumns(columns);
            if (_table is not null) UnhookTable(_table);
            _table = table;
            table.RowChanged += OnTableChanged;
            table.RowDeleted += OnTableChanged;
            Grid.ItemsSource = table.DefaultView;

            FillSeparatorBox(chosen, detected);
            InfoText.Text = $"{rows.Count:N0} rows × {columns} columns";
            IsDirty = false;
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Commits any cell still being edited and returns the grid as text, or
    /// <c>null</c> when nothing changed since the last load/flush.
    /// </summary>
    public string? TakeText()
    {
        if (_table is null) return null;
        // Ends an in-progress cell edit so its value lands in the table.
        Grid.CommitEdit(DataGridEditingUnit.Row, true);
        if (!IsDirty) return null;

        int columns = _table.Columns.Count - 1;
        var rows = new List<IReadOnlyList<string>>();
        foreach (DataRow row in _table.Rows)
        {
            if (row.RowState == DataRowState.Deleted) continue;
            var fields = new string[columns];
            int last = -1;
            for (int c = 0; c < columns; c++)
            {
                fields[c] = row[c] as string ?? "";
                if (fields[c].Length > 0) last = c;
            }
            // Keep ragged rows ragged: never pad beyond what the row had,
            // unless the user typed into a cell further right.
            int count = Math.Max(row[CountColumn] is int n ? n : columns, last + 1);
            rows.Add(fields[..Math.Min(count, columns)]);
        }

        IsDirty = false;
        return Csv.Serialize(rows, _separator, _newline, _trailingNewline);
    }

    /// <summary>Drops the grid's data (tab closed / another tab shown).</summary>
    public void Clear()
    {
        if (_table is not null) UnhookTable(_table);
        _table = null;
        Grid.ItemsSource = null;
        IsDirty = false;
    }

    /// <summary>Puts keyboard focus on the first cell.</summary>
    public void FocusGrid()
    {
        if (Grid.Items.Count > 0 && Grid.Columns.Count > 0)
        {
            Grid.CurrentCell = new DataGridCellInfo(Grid.Items[0], Grid.Columns[0]);
        }
        Grid.Focus();
    }

    private void UnhookTable(DataTable table)
    {
        table.RowChanged -= OnTableChanged;
        table.RowDeleted -= OnTableChanged;
    }

    private void OnTableChanged(object sender, DataRowChangeEventArgs e)
    {
        if (_loading) return;
        IsDirty = true;
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private void BuildColumns(int count)
    {
        Grid.Columns.Clear();
        var editStyle = (Style)Grid.Resources["CellEditBox"];
        for (int c = 0; c < count; c++)
        {
            Grid.Columns.Add(new DataGridTextColumn
            {
                Header = ColumnLetter(c),
                Binding = new Binding("c" + c) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                EditingElementStyle = editStyle,
                MaxWidth = 600
            });
        }
    }

    /// <summary>Spreadsheet-style column names: A..Z, AA, AB, ...</summary>
    private static string ColumnLetter(int index)
    {
        string name = "";
        for (index++; index > 0; index = (index - 1) / 26)
            name = (char)('A' + (index - 1) % 26) + name;
        return name;
    }

    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        int index = e.Row.GetIndex();
        e.Row.Header = index < (_table?.Rows.Count ?? 0) ? (index + 1).ToString() : "*";
        e.Row.FontWeight = index == 0 ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private void FillSeparatorBox(char? chosen, char detected)
    {
        SeparatorBox.Items.Clear();
        SeparatorBox.Items.Add(new ComboBoxItem
        {
            Content = $"Auto — {Csv.NameOf(detected)}",
            Tag = null
        });
        foreach (char sep in Csv.Candidates)
            SeparatorBox.Items.Add(new ComboBoxItem { Content = Csv.NameOf(sep), Tag = sep });

        SeparatorBox.SelectedIndex = chosen is { } c ? Array.IndexOf(Csv.Candidates, c) + 1 : 0;
    }

    private void OnSeparatorSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || SeparatorBox.SelectedItem is not ComboBoxItem item) return;
        SeparatorChosen?.Invoke(this, item.Tag as char?);
    }
}
