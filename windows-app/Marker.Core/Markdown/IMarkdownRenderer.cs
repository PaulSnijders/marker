namespace Marker.Core.Markdown;

/// <summary>Converts markdown source into HTML. v1 wraps Markdig.</summary>
public interface IMarkdownRenderer
{
    /// <summary>Renders markdown to an HTML fragment (no &lt;html&gt; wrapper).</summary>
    string RenderToHtmlFragment(string markdown);
}
