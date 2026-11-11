using System.Text;

namespace Marker.Core.FileTypes;

/// <summary>
/// Minimal RFC 4180-style CSV/TSV reading and writing for the grid view:
/// quoted fields, doubled quotes and newlines inside quotes. No type
/// inference — every cell is a string.
/// </summary>
public static class Csv
{
    /// <summary>Separators the auto-detection considers, most common first.</summary>
    public static readonly char[] Candidates = [',', ';', '\t', '|'];

    /// <summary>
    /// Guesses the separator from the first lines: the candidate that splits
    /// those lines into the same (largest) number of fields wins. Falls back to
    /// tab for <c>.tsv</c> files and comma otherwise.
    /// </summary>
    public static char DetectSeparator(string text, char fallback = ',')
    {
        string sample = text.Length > 64_000 ? text[..64_000] : text;
        char best = fallback;
        int bestScore = 0;
        foreach (char sep in Candidates)
        {
            var rows = Parse(sample, sep).Take(20).ToList();
            if (rows.Count > 1 && sample.Length == 64_000)
                rows.RemoveAt(rows.Count - 1);   // the cut-off row is incomplete
            if (rows.Count == 0) continue;

            int fields = rows[0].Length;
            if (fields < 2) continue;
            int consistent = rows.Count(r => r.Length == fields);
            // Mostly-consistent rows beat a stray separator inside text.
            int score = consistent * 1000 + fields;
            if (consistent * 2 >= rows.Count && score > bestScore)
            {
                best = sep;
                bestScore = score;
            }
        }
        return best;
    }

    /// <summary>Splits the text into rows of fields. A trailing newline does not add an empty row.</summary>
    public static IEnumerable<string[]> Parse(string text, char sep)
    {
        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        bool rowHasContent = false;
        int i = 0;

        while (i < text.Length)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i += 2; continue; }
                    inQuotes = false;
                }
                else field.Append(c);
                i++;
                continue;
            }

            if (c == '"' && field.Length == 0) { inQuotes = true; rowHasContent = true; }
            else if (c == sep) { row.Add(field.ToString()); field.Clear(); rowHasContent = true; }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString());
                field.Clear();
                yield return row.ToArray();
                row.Clear();
                rowHasContent = false;
            }
            else { field.Append(c); rowHasContent = true; }
            i++;
        }

        if (rowHasContent || field.Length > 0)
        {
            row.Add(field.ToString());
            yield return row.ToArray();
        }
    }

    /// <summary>Writes rows back, quoting only fields that need it.</summary>
    public static string Serialize(IEnumerable<IReadOnlyList<string>> rows, char sep, string newline, bool trailingNewline)
    {
        var sb = new StringBuilder();
        bool first = true;
        foreach (var row in rows)
        {
            if (!first) sb.Append(newline);
            first = false;
            for (int c = 0; c < row.Count; c++)
            {
                if (c > 0) sb.Append(sep);
                string v = row[c];
                if (v.IndexOfAny([sep, '"', '\r', '\n']) >= 0)
                    sb.Append('"').Append(v.Replace("\"", "\"\"")).Append('"');
                else
                    sb.Append(v);
            }
        }
        if (trailingNewline && !first) sb.Append(newline);
        return sb.ToString();
    }

    /// <summary>Display name for a separator in the UI.</summary>
    public static string NameOf(char sep) => sep switch
    {
        '\t' => "Tab",
        ',' => "Comma ( , )",
        ';' => "Semicolon ( ; )",
        '|' => "Pipe ( | )",
        _ => sep.ToString()
    };
}
