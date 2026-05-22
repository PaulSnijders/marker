using System.Windows;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace Marker.App.Services;

/// <summary>
/// Registers the custom AvalonEdit highlighting definitions (Markdown, JSON,
/// YAML) shipped as embedded .xshd resources. XML and HTML are already
/// built into AvalonEdit, so they need no registration.
/// </summary>
public static class HighlightingSetup
{
    public static void RegisterCustomDefinitions()
    {
        Register("Markdown", "Assets/Syntax/Markdown.xshd", ".md", ".markdown");
        Register("JSON", "Assets/Syntax/JSON.xshd", ".json");
        Register("YAML", "Assets/Syntax/YAML.xshd", ".yaml", ".yml");
    }

    private static void Register(string name, string resourcePath, params string[] extensions)
    {
        try
        {
            var streamInfo = Application.GetResourceStream(new Uri(resourcePath, UriKind.Relative));
            if (streamInfo is null)
                return;

            using var reader = new XmlTextReader(streamInfo.Stream);
            IHighlightingDefinition definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            HighlightingManager.Instance.RegisterHighlighting(name, extensions, definition);
        }
        catch
        {
            // A malformed definition must not block startup — that file type
            // simply falls back to plain text.
        }
    }
}
