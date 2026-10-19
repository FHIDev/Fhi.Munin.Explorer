using AngleSharp.Dom;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>What a link shows, without the «(åpnes i nytt vindu)» only a screen reader hears.</summary>
internal static class LinkText
{
    public static string Label(this IElement element)
    {
        var copy = (IElement)element.Clone();

        foreach (var hidden in copy.QuerySelectorAll(".screenreader-only").ToList())
        {
            hidden.Remove();
        }

        return copy.TextContent;
    }
}
