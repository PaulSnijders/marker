using System.Net;
using System.Text;

namespace Marker.Core.Markdown;

/// <summary>
/// Detects a leading YAML front matter block (<c>---</c> … <c>---</c>) and turns
/// it into an HTML key/value table. Deliberately a light, forgiving reader of
/// the common subset (scalars, inline and dash lists, indented continuations),
/// not a full YAML parser.
/// </summary>
public static class FrontMatter
{
    /// <summary>
    /// Splits <paramref name="markdown"/> into its front matter YAML (null when
    /// there is none) and the remaining markdown body.
    /// </summary>
    public static (string? Yaml, string Body) Split(string markdown)
    {
        string text = markdown.TrimStart('﻿');
        string[] lines = text.Split('\n');
        if (lines.Length < 2 || lines[0].TrimEnd() != "---")
            return (null, markdown);

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();
            if (line is "---" or "...")
            {
                string yaml = string.Join('\n', lines[1..i]);
                string body = string.Join('\n', lines[(i + 1)..]);
                return (yaml, body);
            }
        }
        return (null, markdown);   // never closed — not front matter
    }

    /// <summary>Renders front matter YAML as an HTML block.</summary>
    public static string ToHtml(string yaml)
    {
        var fields = Parse(yaml);
        var sb = new StringBuilder("<div class=\"frontmatter\">");

        if (fields.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(yaml))
                sb.Append("<pre>").Append(Enc(yaml)).Append("</pre>");
            return sb.Append("</div>\n").ToString();
        }

        sb.Append("<table>");
        foreach (var (key, items, text) in fields)
        {
            sb.Append("<tr><th>").Append(Enc(key)).Append("</th><td>");
            foreach (string item in items)
                sb.Append("<span class=\"fm-tag\">").Append(Enc(item)).Append("</span>");
            if (text.Length > 0)
                sb.Append("<span class=\"fm-text\">").Append(Enc(text)).Append("</span>");
            sb.Append("</td></tr>");
        }
        return sb.Append("</table></div>\n").ToString();
    }

    private static List<(string Key, List<string> Items, string Text)> Parse(string yaml)
    {
        var fields = new List<(string Key, List<string> Items, StringBuilder Text)>();

        foreach (string raw in yaml.Split('\n'))
        {
            string line = raw.TrimEnd('\r', ' ', '\t');
            if (line.Length == 0 || line.TrimStart().StartsWith('#'))
                continue;

            bool indented = char.IsWhiteSpace(line[0]) || line.StartsWith("- ") || line == "-";
            int colon = line.IndexOf(':');

            if (!indented && colon > 0)
            {
                string key = line[..colon].Trim();
                string value = line[(colon + 1)..].Trim();
                var items = new List<string>();
                var text = new StringBuilder();

                if (value.StartsWith('[') && value.EndsWith(']'))
                    items.AddRange(value[1..^1].Split(',')
                        .Select(s => Unquote(s.Trim())).Where(s => s.Length > 0));
                else if (value is not ("|" or ">" or "|-" or ">-" or "|+" or ">+"))
                    text.Append(Unquote(value));

                fields.Add((key, items, text));
            }
            else if (fields.Count > 0)
            {
                var (_, items, text) = fields[^1];
                string trimmed = line.Trim();
                if (trimmed.StartsWith("- ") && text.Length == 0)
                    items.Add(Unquote(trimmed[2..].Trim()));
                else
                {
                    if (text.Length > 0) text.Append('\n');
                    text.Append(trimmed);
                }
            }
        }

        return fields.Select(f => (f.Key, f.Items, f.Text.ToString())).ToList();
    }

    private static string Unquote(string s)
        => s.Length >= 2 && (s[0] == '"' || s[0] == '\'') && s[^1] == s[0] ? s[1..^1] : s;

    private static string Enc(string s) => WebUtility.HtmlEncode(s);
}
