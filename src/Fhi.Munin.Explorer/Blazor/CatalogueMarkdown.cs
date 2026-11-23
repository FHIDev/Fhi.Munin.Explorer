using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Renders the catalogue's authored text: markdown links become anchors, line breaks become
/// <c>&lt;br&gt;</c>, and, with CatalogueFormatting on, emphasis strong or em and lists ul or ol with li; the rest stays text.
/// </summary>
/// <remarks>
/// The catalogue's beskrivelse and hjemmeside fields are authored with markdown links and
/// <c>&lt;br&gt;</c> tags, which the views used to print as source (FHIDev/Munin#5385). Rendering
/// them is a decision about trust, and the shape here is Kelda's — render markdown, never render
/// HTML — taken one step further because this component is embedded on helsedata.no: the Markdig
/// AST is walked into the <see cref="RenderTreeBuilder"/> directly, so no markdown-to-HTML string
/// and no <see cref="MarkupString"/> exist anywhere in the path. Catalogue text can only produce
/// the elements this class writes a case for, named in the summary, and every text node goes
/// through Blazor's own encoding. A construct without a case (raw HTML, a heading, <c>_</c> emphasis, a
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

    /// <summary>An http(s) address in running text, without the sentence punctuation after it.</summary>
    [GeneratedRegex(@"(?<![\w/])https?://(?:[^\s<>""';]|;(?!https?://))+?(?=[.,;:!?)\]'""]*(?:\s|;(?=https?://)|$))", RegexOptions.IgnoreCase, 250)]
    private static partial Regex WebAddress();

    /// <summary>The schemes a link is allowed to carry; anything else renders as text.</summary>
    internal static bool AllowedScheme(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https" or "mailto";

    /// <summary>
    /// One valid e-mail address as its <c>mailto:</c> href, by Munin's Email rule; <c>%</c> is refused since a mail
    /// client decodes it, and the rule admits no quote, <c>?</c> or <c>#</c>.
    /// </summary>
    internal static string? Mailto(string? raw)
    {
        if (raw?.Trim() is not { Length: > 0 } address || address.Contains('%'))
        {
            return null;
        }

        try
        {
            return EmailPattern().IsMatch(address) ? "mailto:" + address : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.None, 250)]
    private static partial Regex EmailPattern();

    /// <summary>
    /// A whole value that is one link — <c>[label](url)</c> or a bare URL — as label and href, or
    /// nothing where it is anything else.
    /// </summary>
    /// <remarks>
    /// For the fields the catalogue declares to be a <c>Url</c>, where making the value followable
    /// is a type-driven parse rather than markdown rendering: the answer is one anchor, never a
    /// fragment, so a value that is not exactly one allowed link stays the text it always was —
    /// except that a trailing <c>;</c> after a single address is dropped from both label and href.
    /// </remarks>
    internal static (string Label, string Href)? Link(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();

        // A ';'-joined list parses as one absolute URI too, and that address exists nowhere.
        if (LinkList(trimmed) is not null)
        {
            return null;
        }

        // The list's separator left behind after its only address, which no href should carry.
        var address = trimmed.TrimEnd(';').TrimEnd();

        if (!address.Any(char.IsWhiteSpace))
        {
            if (AllowedScheme(address))
            {
                return (address, address);
            }

            // The catalogue also stores Hjemmeside scheme-less - www.barnediabetes.no - which an
            // href would treat as a relative path. https is assumed for the address; the label
            // stays the stored text.
            if (address.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                && AllowedScheme($"https://{address}"))
            {
                return (address, $"https://{address}");
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

    /// <summary>
    /// A whole value that is two or more http(s) addresses joined by <c>;</c>, in order, or nothing
    /// where any part is not one. It splits on every <c>;</c>, so a query whose text after a
    /// <c>;</c> is itself an address splits there too.
    /// </summary>
    internal static IReadOnlyList<string>? LinkList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !raw.Contains(';'))
        {
            return null;
        }

        var parts = raw.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return parts.Length >= 2 && parts.All(IsWebAddress) ? parts : null;
    }

    // A list is web addresses only; mailto stays a single-link case.
    private static bool IsWebAddress(string part) =>
        !part.Any(char.IsWhiteSpace)
        && AllowedScheme(part)
        && !part.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    /// <summary>A value's words: the label where the whole value is one allowed link, else the value.</summary>
    internal static string? Words(string? raw) => Link(raw)?.Label ?? raw;

    // A ;-joined list of addresses stays text here; only a URL-typed property links each (Fhi.Metadata-61s28).
    internal static bool HasWebAddress(string? raw) =>
        raw is not null && LinkList(raw) is null && Addresses(raw).Count > 0;

    /// <summary>Each address as start and length; the ")"s closing "("s inside it are kept.</summary>
    private static List<(int Index, int Length)> Addresses(string raw)
    {
        try
        {
            return WebAddress().Matches(raw)
                .Where(match => AllowedScheme(match.Value))
                .Select(match =>
                {
                    var length = match.Length;
                    var open = match.Value.Count(c => c == '(') - match.Value.Count(c => c == ')');
                    while (open > 0 && match.Index + length < raw.Length && raw[match.Index + length] == ')')
                    {
                        length++;
                        open--;
                    }

                    return (match.Index, length);
                })
                .ToList();
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }
    }

    /// <summary>
    /// A value whose http(s) addresses are anchors and the rest text, or its one anchor where the
    /// whole value is a link. No markdown and no HTML: the text around an address stays text.
    /// </summary>
    internal static RenderFragment Linked(string? raw) => builder =>
    {
        if (string.IsNullOrWhiteSpace(raw) || LinkList(raw) is not null)
        {
            builder.AddContent(6, raw);
            return;
        }

        if (Link(raw) is { } whole)
        {
            WebAnchor(builder, whole.Href, whole.Label);
            return;
        }

        var at = 0;
        foreach (var (index, length) in Addresses(raw))
        {
            var address = raw.Substring(index, length);
            builder.AddContent(0, raw[at..index]);
            WebAnchor(builder, address, address);
            at = index + length;
        }

        builder.AddContent(5, raw[at..]);
    };

    private static void WebAnchor(RenderTreeBuilder builder, string href, string label)
    {
        builder.OpenComponent<CatalogueLink>(1);
        builder.AddComponentParameter(2, nameof(CatalogueLink.Href), href);
        builder.AddComponentParameter(3, nameof(CatalogueLink.ChildContent), (RenderFragment)(inner => inner.AddContent(0, label)));
        builder.CloseComponent();
    }

    /// <summary>Whether a link's label is only its own address, which is prose in no language (WCAG 3.1.2).</summary>
    internal static bool IsAddress((string Label, string Href) link) =>
        link.Href == link.Label || link.Href == $"https://{link.Label}" || link.Href == $"http://{link.Label}";

    /// <summary>Whether a value's words are the catalogue's prose rather than an address.</summary>
    internal static bool Prose(string? raw) =>
        LinkList(raw) is null && (Link(raw) is not { } link || !IsAddress(link));

    /// <summary>The catalogue text as a fragment: anchors, breaks, and literal text for the rest; <c>formatted</c>
    /// also draws <c>*</c> emphasis and <c>-</c> and <c>1.</c> lists as elements.</summary>
    internal static RenderFragment Render(string? text, bool formatted = false) => builder =>
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var source = Source(text);
        var seq = 0;

        // The cap answers for the stored value, so it is the stored length that is measured:
        // normalising first would let 24 000 characters of <br> shrink under it and be parsed.
        if (text.Length > MaxParsedLength)
        {
            PlainLines(builder, ref seq, source);
            return;
        }

        Blocks(builder, ref seq, Drawn(Markdown.Parse(source, Pipeline), formatted), source, formatted);
    };

    /// <summary>Whether <see cref="Render"/> draws a list element for the text, which no <c>p</c> or <c>span</c> may hold.</summary>
    internal static bool DrawsList(string? text, bool formatted) =>
        formatted
        && !string.IsNullOrWhiteSpace(text)
        && text.Length <= MaxParsedLength
        && Markdown.Parse(Source(text), Pipeline).Descendants<ListBlock>().Any(Formatted);

    /// <summary>The text's top-level blocks as spans of its normalised source, so a cut can fall between them.</summary>
    internal static (string Source, IReadOnlyList<(SourceSpan Span, bool IsList)> Blocks) TopBlocks(string text)
    {
        var source = Source(text);

        return (source, Markdown.Parse(source, Pipeline)
            .Where(block => block is not LinkReferenceDefinitionGroup)
            .Select(block => (block.Span, block is ListBlock))
            .ToList());
    }

    /// <summary>Where the emphasis Render draws as elements sits in a paragraph, so a cut can stay out of it.</summary>
    internal static IReadOnlyList<SourceSpan> EmphasisSpans(string paragraph) =>
        paragraph.Length > MaxParsedLength
            ? []
            : Markdown.Parse(Source(paragraph), Pipeline).Descendants<EmphasisInline>().Where(Emphasised).Select(e => e.Span).ToList();

    private static string Source(string text) => BrTag().Replace(text.Replace("\r\n", "\n"), "\n");

    /// <summary>Only the two list kinds the catalogue writes, three deep and with every item holding text; <c>*</c>,
    /// <c>+</c>, <c>1)</c>, a lone <c>-</c> placeholder and deeper lists stay lines of text, as quoted ones do.</summary>
    private static bool Formatted(ListBlock list) =>
        (list.IsOrdered ? list.OrderedDelimiter == '.' : list.BulletType == '-')
        && list.OfType<ListItemBlock>().All(item => item.Count > 0)
        && Ancestors(list).Count(block => block is ListBlock) < MaxDepth
        && !Ancestors(list).Any(block => block is QuoteBlock);

    /// <summary>How deep lists or emphasis may nest as elements; editable master data must not build a deep tree.</summary>
    private const int MaxDepth = 3;

    private static IEnumerable<Block> Ancestors(Block block)
    {
        for (var parent = block.Parent; parent is not null; parent = parent.Parent)
        {
            yield return parent;
        }
    }

    private static bool IsList(Block block, bool formatted) => formatted && block is ListBlock list && Formatted(list);

    /// <summary>Blocks one after another, a blank line between them except beside a list, which is a block of its own.</summary>
    private static void Blocks(RenderTreeBuilder builder, ref int seq, IEnumerable<Block> blocks, string source, bool formatted)
    {
        Block? previous = null;

        foreach (var block in blocks)
        {
            if (previous is not null && !IsList(previous, formatted) && !IsList(block, formatted))
            {
                Break(builder, ref seq);
                Break(builder, ref seq);
            }

            previous = block;
            Block(builder, ref seq, block, source, formatted);
        }
    }

    /// <summary>The blocks in source order, each reference definition drawn as its source unless a drawn
    /// anchor took its URL. Markdig files the definitions in one group whose span means nothing.</summary>
    private static IEnumerable<Block> Drawn(MarkdownDocument document, bool formatted)
    {
        var lent = Paragraphs(document)
            .SelectMany(paragraph => paragraph.Inline is { } inlines ? Anchored(inlines, formatted) : [])
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
    private static IEnumerable<LinkInline> Anchored(ContainerInline container, bool formatted) =>
        container.SelectMany(inline => inline switch
        {
            LinkInline { IsImage: false } link when AllowedScheme(link.Url) => [link],
            DelimiterInline delimiter => Anchored(delimiter, formatted),
            EmphasisInline emphasis when formatted && Emphasised(emphasis) => Anchored(emphasis, formatted),
            _ => Enumerable.Empty<LinkInline>(),
        });

    /// <summary>Only <c>*</c> emphasis: <c>_</c> is in variable names and code strings far more often than in prose.</summary>
    private static bool Emphasised(EmphasisInline emphasis) =>
        emphasis is { DelimiterChar: '*', DelimiterCount: 1 or 2 } && EmphasisDepth(emphasis) < MaxDepth;

    private static int EmphasisDepth(Inline inline)
    {
        var depth = 0;

        for (var parent = inline.Parent; parent is not null; parent = parent.Parent)
        {
            depth += parent is EmphasisInline ? 1 : 0;
        }

        return depth;
    }

    /// <summary>Whether an item's children are walked, or the item is drawn as its source.</summary>
    private static bool Walked(ListItemBlock item) => item.Count > 0 && item[0].Span.Start > item.Span.Start;

    private static void Block(RenderTreeBuilder builder, ref int seq, Block block, string source, bool formatted)
    {
        switch (block)
        {
            case ParagraphBlock { Inline: { } inlines }:
                Inlines(builder, ref seq, inlines, source, formatted);
                break;
            case ListBlock list when IsList(list, formatted):
                List(builder, ref seq, list, source);
                break;
            case ListBlock list:
                ListItems(builder, ref seq, list, source, formatted);
                break;
            default:
                PlainLines(builder, ref seq, Sliced(source, block.Span));
                break;
        }
    }

    /// <summary>A list as a <c>ul</c> or an <c>ol</c>, each item's blocks inside its <c>li</c>.</summary>
    private static void List(RenderTreeBuilder builder, ref int seq, ListBlock list, string source)
    {
        builder.OpenElement(seq++, list.IsOrdered ? "ol" : "ul");
        builder.AddAttribute(seq++, "class", ListClass);

        if (list.IsOrdered && int.TryParse(list.OrderedStart, out var start) && start != 1)
        {
            builder.AddAttribute(seq++, "start", start);
        }

        foreach (var item in list.OfType<ListItemBlock>())
        {
            builder.OpenElement(seq++, "li");

            if (Walked(item))
            {
                Blocks(builder, ref seq, item, source, formatted: true);
            }
            else
            {
                PlainLines(builder, ref seq, Sliced(source, item.Span));
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    }

    /// <summary>The handle a host styles the lists by; the detail panel's own <c>dd ol</c> is a trail.</summary>
    internal const string ListClass = "munin-explorer-text__list";

    /// <summary>A list as the lines it was written in, its markers literal and its items' links live.</summary>
    private static void ListItems(RenderTreeBuilder builder, ref int seq, ListBlock list, string source, bool formatted)
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

            Block? previous = null;

            foreach (var child in item)
            {
                if (previous is not null)
                {
                    Break(builder, ref seq);

                    // A blank line between an item's blocks is kept, as it is between the blocks of a document.
                    if (previous.Span.End < child.Span.Start
                        && source[previous.Span.End..child.Span.Start].Count(c => c == '\n') > 1)
                    {
                        Break(builder, ref seq);
                    }
                }

                previous = child;
                Block(builder, ref seq, child, source, formatted);
            }
        }
    }

    private static void Inlines(RenderTreeBuilder builder, ref int seq, ContainerInline container, string source, bool formatted)
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
                    Anchor(builder, ref seq, link, source, formatted);
                    break;
                case EmphasisInline emphasis when formatted && Emphasised(emphasis):
                    builder.OpenElement(seq++, emphasis.DelimiterCount == 2 ? "strong" : "em");
                    Inlines(builder, ref seq, emphasis, source, formatted);
                    builder.CloseElement();
                    break;
                // An unmatched [ or ![ spans only itself and holds the text after it as children.
                case DelimiterInline delimiter:
                    PlainLines(builder, ref seq, Sliced(source, delimiter.Span));
                    Inlines(builder, ref seq, delimiter, source, formatted);
                    break;
                default:
                    PlainLines(builder, ref seq, Sliced(source, inline.Span));
                    break;
            }
        }
    }

    private static void Anchor(RenderTreeBuilder builder, ref int seq, LinkInline link, string source, bool formatted)
    {
        builder.OpenComponent<CatalogueLink>(seq++);
        builder.AddComponentParameter(seq++, nameof(CatalogueLink.Href), link.Url ?? "");
        builder.AddComponentParameter(seq++, nameof(CatalogueLink.ChildContent), (RenderFragment)(inner =>
        {
            var at = 0;

            if (link.FirstChild is null)
            {
                inner.AddContent(at, link.Url);
            }
            else
            {
                Inlines(inner, ref at, link, source, formatted);
            }
        }));
        builder.CloseComponent();
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
