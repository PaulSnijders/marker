using Markdig;

namespace Marker.Core.Markdown;

/// <summary><see cref="IMarkdownRenderer"/> implemented with Markdig.</summary>
public sealed class MarkdigRenderer : IMarkdownRenderer
{
    // Built once and reused; the pipeline is immutable and thread-safe.
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()   // tables, task lists, footnotes, auto-links, ...
        .UseSoftlineBreakAsHardlineBreak()
        .Build();

    public string RenderToHtmlFragment(string markdown)
        => Markdig.Markdown.ToHtml(markdown ?? string.Empty, _pipeline);
}
