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
    internal static bool AllowedScheme(string? url) =>
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

    /// <summary>A value's words: the label where the whole value is one allowed link, else the value.</summary>
    internal static string? Words(string? raw) => Link(raw)?.Label ?? raw;

    /// <summary>Whether a link's label is only its own address, which is prose in no language (WCAG 3.1.2).</summary>
    internal static bool IsAddress((string Label, string Href) link) =>
        link.Href == link.Label || link.Href == $"https://{link.Label}";

    /// <summary>Whether a value's words are the catalogue's prose rather than an address.</summary>
    internal static bool Prose(string? raw) => Link(raw) is not { } link || !IsAddress(link);

    /// <summary>The catalogue text as a fragment: anchors, breaks, and literal text for the rest.</summary>
    internal static RenderFragment Render(string? text) => builder =>
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var source = BrTag().Replace(text.Replace("\r\n", "\n"), "\n");
        var seq = 0;

        // The cap answers for the stored value, so it is the stored length that is measured:
        // normalising first would let 24 000 characters of <br> shrink under it and be parsed.
        if (text.Length > MaxParsedLength)
        {
            PlainLines(builder, ref seq, source);
            return;
        }

        var first = true;

        foreach (var block in Drawn(Markdown.Parse(source, Pipeline)))
        {
            if (!first)
            {
                Break(builder, ref seq);
                Break(builder, ref seq);
            }

            first = false;
            Block(builder, ref seq, block, source);
        }
    };

    /// <summary>The blocks in source order, each reference definition drawn as its source unless a drawn
    /// anchor took its URL. Markdig files the definitions in one group whose span means nothing.</summary>
    private static IEnumerable<Block> Drawn(MarkdownDocument document)
    {
        var lent = Paragraphs(document)
            .SelectMany(paragraph => paragraph.Inline is { } inlines ? Anchored(inlines) : [])
            .Select(link => link.Reference)
            .OfType<LinkReferenceDefinition>()
            .ToHashSet();

        var sliced = SlicedSpans(document).ToList();

        // Markdig keeps a paragraph's span over the definitions it lifted out of it, so a paragraph
        // is placed by where its own text starts.
        return document
            .SelectMany(block => block is LinkReferenceDefinitionGroup group
                ? group.OfType<LinkReferenceDefinition>()
                       .Where(definition => !lent.Contains(definition)
                                            && !sliced.Any(span => Covers(span, definition.Span)))
                : Enumerable.Repeat(block, 1))
            .OrderBy(block => block is ParagraphBlock { Inline.FirstChild: { } first } ? first.Span.Start : block.Span.Start);
    }

    /// <summary>The spans <see cref="Block"/> draws as source text, where a definition already shows.</summary>
    private static IEnumerable<SourceSpan> SlicedSpans(ContainerBlock container) =>
        container.SelectMany(block => block switch
        {
            LinkReferenceDefinitionGroup or ParagraphBlock => Enumerable.Empty<SourceSpan>(),
            ListBlock list => list.OfType<ListItemBlock>().SelectMany(item => Walked(item)
                ? SlicedSpans(item).Prepend(new SourceSpan(item.Span.Start, item[0].Span.Start - 1))
                : [item.Span]),
            _ => [block.Span],
        });

    private static bool Covers(SourceSpan outer, SourceSpan inner) =>
        inner.Start >= outer.Start && inner.Start <= outer.End;

    /// <summary>The paragraphs <see cref="Block"/> walks inline by inline, and no others.</summary>
    private static IEnumerable<ParagraphBlock> Paragraphs(ContainerBlock container) =>
        container.SelectMany(block => block switch
        {
            ParagraphBlock paragraph => [paragraph],
            ListBlock list => list.OfType<ListItemBlock>().Where(Walked).SelectMany(Paragraphs),
            _ => Enumerable.Empty<ParagraphBlock>(),
        });

    /// <summary>The links <see cref="Inlines"/> draws as anchors, found by the same cases.</summary>
    private static IEnumerable<LinkInline> Anchored(ContainerInline container) =>
        container.SelectMany(inline => inline switch
        {
            LinkInline { IsImage: false } link when AllowedScheme(link.Url) => [link],
            DelimiterInline delimiter => Anchored(delimiter),
            _ => Enumerable.Empty<LinkInline>(),
        });

    /// <summary>Whether an item's children are walked, or the item is drawn as its source.</summary>
    private static bool Walked(ListItemBlock item) => item.Count > 0 && item[0].Span.Start > item.Span.Start;

    private static void Block(RenderTreeBuilder builder, ref int seq, Block block, string source)
    {
        switch (block)
        {
            case ParagraphBlock { Inline: { } inlines }:
                Inlines(builder, ref seq, inlines, source);
                break;
            case ListBlock list:
                ListItems(builder, ref seq, list, source);
                break;
            default:
                PlainLines(builder, ref seq, Sliced(source, block.Span));
                break;
        }
    }

    /// <summary>A list as the lines it was written in, its markers literal and its items' links live.</summary>
    private static void ListItems(RenderTreeBuilder builder, ref int seq, ListBlock list, string source)
    {
        var firstItem = true;

        foreach (var item in list.OfType<ListItemBlock>())
        {
            if (!firstItem)
            {
                Break(builder, ref seq);

                if (list.IsLoose)
                {
                    Break(builder, ref seq);
                }
            }

            firstItem = false;

            if (!Walked(item))
            {
                PlainLines(builder, ref seq, Sliced(source, item.Span));
                continue;
            }

            PlainLines(builder, ref seq, source[item.Span.Start..item[0].Span.Start]);

            var firstChild = true;

            foreach (var child in item)
            {
                if (!firstChild)
                {
                    Break(builder, ref seq);
                }

                firstChild = false;
                Block(builder, ref seq, child, source);
            }
        }
    }

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
                // An unmatched [ or ![ spans only itself and holds the text after it as children.
                case DelimiterInline delimiter:
                    PlainLines(builder, ref seq, Sliced(source, delimiter.Span));
                    Inlines(builder, ref seq, delimiter, source);
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
