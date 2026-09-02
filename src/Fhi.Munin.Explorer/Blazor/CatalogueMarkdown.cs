using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Renders the catalogue's authored text: markdown links become anchors, line breaks become
/// <c>&lt;br&gt;</c>, and everything else stays literal text.
/// </summary>
/// <remarks>
/// The catalogue's beskrivelse and hjemmeside fields are authored with markdown links and
/// <c>&lt;br&gt;</c> tags, which the views used to print as source (FHIDev/Munin#5385). Rendering
/// them is a decision about trust, and the shape here is Kelda's — render markdown, never render
/// HTML — taken one step further because this component is embedded on helsedata.no: the Markdig
/// AST is walked into the <see cref="RenderTreeBuilder"/> directly, so no markdown-to-HTML string
/// and no <see cref="MarkupString"/> exist anywhere in the path. Catalogue text can only produce
/// the elements this class writes a case for — an anchor or a break — and every text node goes
/// through Blazor's own encoding. A construct without a case (raw HTML, a heading, emphasis, a
/// code block, a <c>javascript:</c> link) renders as its literal source text, exactly as the whole
/// value did before.
/// </remarks>
internal static partial class CatalogueMarkdown
{
    /// <summary>
    /// The longest text worth parsing. Beskrivelser are editable master data, so a pathological
    /// value must not be able to stall a reader's circuit; past the cap the text renders as plain
    /// lines. The longest description measured is under 3 000 characters.
    /// </summary>
    internal const int MaxParsedLength = 20_000;

    /// <summary>
    /// Core CommonMark only — no extensions, so nothing linkifies or builds tables. Precise source
    /// locations are what let a construct without a case render as its own source text; without
    /// them an inline's span is empty and the fallback would swallow the text instead.
    /// </summary>
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UsePreciseSourceLocation().Build();

    /// <summary>
    /// The catalogue's <c>&lt;br&gt;</c>s, rewritten to newlines before parsing — Kelda's move,
    /// which is what keeps raw HTML rendering off: the one tag the catalogue actually uses stops
    /// being HTML before the parser sees it.
    /// </summary>
    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BrTag();

    /// <summary>The schemes a link is allowed to carry; anything else renders as text.</summary>
    private static bool AllowedScheme(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https" or "mailto";

    /// <summary>
    /// A whole value that is one link — <c>[label](url)</c> or a bare URL — as label and href, or
    /// nothing where it is anything else.
    /// </summary>
    /// <remarks>
    /// For the fields the catalogue declares to be a <c>Url</c>, where making the value followable
    /// is a type-driven parse rather than markdown rendering: the answer is one anchor, never a
    /// fragment, so a value that is not exactly one allowed link stays the text it always was.
    /// </remarks>
    internal static (string Label, string Href)? Link(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();

        if (!trimmed.Any(char.IsWhiteSpace))
        {
            if (AllowedScheme(trimmed))
            {
                return (trimmed, trimmed);
            }

            // The catalogue also stores Hjemmeside scheme-less - www.barnediabetes.no - which an
            // href would treat as a relative path. https is assumed for the address; the label
            // stays the stored text.
            if (trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                && AllowedScheme($"https://{trimmed}"))
            {
                return (trimmed, $"https://{trimmed}");
            }
        }

        if (trimmed.Length > MaxParsedLength)
        {
            return null;
        }

        if (Markdown.Parse(trimmed, Pipeline) is not [ParagraphBlock { Inline: { } inlines }]
            || inlines.FirstChild is not LinkInline { IsImage: false, NextSibling: null } link
            || !AllowedScheme(link.Url))
        {
            return null;
        }

        var label = string.Concat(link.OfType<LiteralInline>().Select(l => l.Content.ToString())).Trim();

        return (label.Length > 0 ? label : link.Url!, link.Url!);
    }

    /// <summary>The catalogue text as a fragment: anchors, breaks, and literal text for the rest.</summary>
    internal static RenderFragment Render(string? text) => builder =>
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var source = BrTag().Replace(text.Replace("\r\n", "\n"), "\n");
        var seq = 0;

        if (source.Length > MaxParsedLength)
        {
            PlainLines(builder, ref seq, source);
            return;
        }

        var first = true;

        foreach (var block in Markdown.Parse(source, Pipeline))
        {
            if (!first)
            {
                Break(builder, ref seq);
                Break(builder, ref seq);
            }

            first = false;

            if (block is ParagraphBlock { Inline: { } inlines })
            {
                Inlines(builder, ref seq, inlines, source);
            }
            else
            {
                PlainLines(builder, ref seq, Sliced(source, block.Span));
            }
        }
    };

    private static void Inlines(RenderTreeBuilder builder, ref int seq, ContainerInline container, string source)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    builder.AddContent(seq++, literal.Content.ToString());
                    break;
                case LineBreakInline:
                    // Soft breaks too: 46 of the 66 kilder measured separate their paragraphs with
                    // plain newlines, which HTML would otherwise collapse into the running text.
                    Break(builder, ref seq);
                    break;
                case HtmlEntityInline entity:
                    builder.AddContent(seq++, entity.Transcoded.ToString());
                    break;
                case LinkInline { IsImage: false } link when AllowedScheme(link.Url):
                    Anchor(builder, ref seq, link, source);
                    break;
                default:
                    PlainLines(builder, ref seq, Sliced(source, inline.Span));
                    break;
            }
        }
    }

    private static void Anchor(RenderTreeBuilder builder, ref int seq, LinkInline link, string source)
    {
        builder.OpenElement(seq++, "a");
        builder.AddAttribute(seq++, "href", link.Url);
        builder.AddAttribute(seq++, "rel", "noopener noreferrer");

        if (link.FirstChild is null)
        {
            builder.AddContent(seq++, link.Url);
        }
        else
        {
            Inlines(builder, ref seq, link, source);
        }

        builder.CloseElement();
    }

    private static void Break(RenderTreeBuilder builder, ref int seq)
    {
        builder.OpenElement(seq++, "br");
        builder.CloseElement();
    }

    /// <summary>Text as-is, with its newlines as breaks — for everything without a case above.</summary>
    private static void PlainLines(RenderTreeBuilder builder, ref int seq, string text)
    {
        var firstLine = true;

        foreach (var line in text.Split('\n'))
        {
            if (!firstLine)
            {
                Break(builder, ref seq);
            }

            firstLine = false;
            builder.AddContent(seq++, line);
        }
    }

    private static string Sliced(string source, SourceSpan span) =>
        span.Start >= 0 && span.End >= span.Start && span.End < source.Length
            ? source[span.Start..(span.End + 1)]
            : "";
}
