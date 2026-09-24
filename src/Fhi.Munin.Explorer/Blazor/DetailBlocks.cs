using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The pieces every detail view is built from: a heading at a level the caller picks, a definition
/// list of facts, and one group of the catalogue's own properties.
/// </summary>
/// <remarks>
/// Shared by <see cref="KildeView"/>, <see cref="VariableView"/>, <see cref="DatasamlingView"/> and
/// <see cref="InstrumentView"/>, the first three of which drew it from three private copies. Level
/// and class stay the caller's, as they are for <see cref="StatisticsBlock"/>: the same block sits
/// at three different depths.
/// <para>
/// <see cref="Values"/> alone is shared with the result row's drill-in panel, which is a
/// different surface wearing a different prefix — hence the class it takes.
/// </para>
/// </remarks>
internal static class DetailBlocks
{
    // The chassis's own fact list, not the drill-in panel's: Values is the one piece both
    // surfaces draw, so its class comes from the caller and only a detail page asks for these.
    // (Fhi.Metadata-35w0p.11)
    private const string PageFields = "munin-explorer-page__fields";
    private const string PageLanguage = "munin-explorer-page__language";

    /// <summary>The one class that mutes what the catalogue holds nothing for, on a fact or a whole tab.</summary>
    internal const string Absent = "munin-explorer-absent";

    /// <summary>The class on the sentence that opens the kodeverk block, in both places that block is drawn.</summary>
    internal const string Lead = "munin-explorer-lead";

    /// <summary>A block's lead sentence under its heading, or nothing where it has none.</summary>
    internal static RenderFragment LeadParagraph(string? text) => builder =>
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", Lead);
        builder.AddContent(2, text);
        builder.CloseElement();
    };

    /// <summary>A heading at the given level, so a view nests wherever it is put.</summary>
    internal static RenderFragment Heading(int level, string text, string cssClass,
                                           string? id = null, string? language = null) => builder =>
    {
        builder.OpenElement(0, $"h{level}");
        builder.AddAttribute(1, "class", cssClass);
        builder.AddAttribute(2, "id", id);
        builder.AddAttribute(3, "lang", language);
        builder.AddContent(4, text);
        builder.CloseElement();
    };

    /// <summary>
    /// A definition list of label and value, where a value the catalogue holds nothing for reads
    /// <see cref="Texts.NoValue"/>, muted — and no list at all when it holds nothing for any row.
    /// </summary>
    /// <remarks>
    /// The emptiness question is answered here rather than at each call site, for the reason
    /// <see cref="StatisticsBlock"/> gives: a heading over an empty list passes any test written
    /// with rich data only. A row that should not be drawn at all is the caller's to leave out —
    /// see <see cref="CataloguePlacement.UnlessPlaced"/>.
    /// </remarks>
    internal static RenderFragment Facts(
        IReadOnlyList<(string Label, string? Value, bool Norwegian)> facts, string? language,
        bool authored = false) => LinkedFacts(Unlinked(facts), language, authored);

    /// <summary>
    /// The same list, where a row's value can be somewhere the reader goes.
    /// </summary>
    /// <remarks>
    /// A target the surface above hands down, as <see cref="DetailTrailStep.Href"/> is: this package
    /// has no router, so a null <c>Href</c> is a row drawn as plain text rather than as a link that
    /// goes nowhere.
    /// <para>
    /// A name of its own rather than an overload of <see cref="Facts"/>: every <c>cref</c> to either
    /// would be ambiguous, and a doc comment is not worth a signature spelled out in one.
    /// </para>
    /// </remarks>
    internal static RenderFragment LinkedFacts(
        IReadOnlyList<(string Label, string? Value, bool Norwegian, string? Href)> facts, string? language,
        bool authored = false) => builder =>
    {
        if (Shown(facts).Count == 0)
        {
            return;
        }

        var reader = ReaderLanguage.Of(language);
        var text = Texts.For(language);

        builder.OpenElement(0, "dl");
        builder.AddAttribute(1, "class", PageFields);

        var seq = 10;

        foreach (var (label, value, norwegian, href) in facts)
        {
            builder.OpenElement(seq, "div");

            builder.OpenElement(seq + 1, "dt");
            builder.AddAttribute(seq + 2, "class", "headline headline-xxs margin--none");
            builder.AddContent(seq + 3, label);
            builder.CloseElement();

            var absent = string.IsNullOrWhiteSpace(value);

            // An absent value is our word rather than the catalogue's, so it is unmarked and goes nowhere.
            builder.OpenElement(seq + 4, "dd");
            builder.AddAttribute(seq + 5, "lang", norwegian && !absent ? CatalogueProperties.Foreign("no", reader) : null);
            builder.AddAttribute(seq + 6, "class", absent ? Absent : null);

            if (absent)
            {
                builder.AddContent(seq + 7, text.NoValue);
            }
            // A linked row is never rendered as markdown: a target and authored prose are two ways
            // to spend one dd, and the rule here is that the one a reader can press wins.
            else if (href is not null)
            {
                builder.OpenElement(seq + 8, "a");
                builder.AddAttribute(seq + 9, "href", href);
                builder.AddAttribute(seq + 10, "rel",
                                     CatalogueMarkdown.AllowedScheme(href) ? "noopener noreferrer" : null);
                builder.AddContent(seq + 13, value);
                builder.CloseElement();
            }
            else if (authored)
            {
                builder.AddContent(seq + 11, CatalogueMarkdown.Render(value));
            }
            else
            {
                builder.AddContent(seq + 12, value);
            }

            builder.CloseElement();

            builder.CloseElement();

            // Twenty rather than ten: a row's dd branches run to seq + 13, past where the next row
            // began under the old stride. No rendered markup differs either way — the diff
            // tolerates the repeat — so this is the numbering contract kept, not a defect fixed.
            seq += 20;
        }

        builder.CloseElement();
    };

    /// <summary>
    /// Whether <see cref="Facts"/> would draw a row, so a caller can drop the heading over it too.
    /// </summary>
    /// <remarks>
    /// Both answers come off <see cref="Shown"/>, because a section wrapper decided here and a list
    /// decided there could otherwise disagree and leave an empty <c>section</c> behind.
    /// </remarks>
    internal static bool AnyFacts(IReadOnlyList<(string Label, string? Value, bool Norwegian)> facts) =>
        Shown(Unlinked(facts)).Count > 0;

    /// <summary>The same question about a list that carries targets — see <see cref="LinkedFacts"/>.</summary>
    internal static bool AnyLinkedFacts(
        IReadOnlyList<(string Label, string? Value, bool Norwegian, string? Href)> facts) =>
        Shown(facts).Count > 0;

    private static List<(string Label, string? Value, bool Norwegian, string? Href)> Shown(
        IReadOnlyList<(string Label, string? Value, bool Norwegian, string? Href)> facts) =>
        [.. facts.Where(f => !string.IsNullOrWhiteSpace(f.Value))];

    /// <summary>A list of facts none of which goes anywhere, which is most of them.</summary>
    private static List<(string Label, string? Value, bool Norwegian, string? Href)> Unlinked(
        IReadOnlyList<(string Label, string? Value, bool Norwegian)> facts) =>
        [.. facts.Select(f => (f.Label, f.Value, f.Norwegian, (string?)null))];

    /// <summary>
    /// A row's value as one <c>dd</c> per language the catalogue holds it in, and the sequence
    /// number the caller continues from.
    /// </summary>
    /// <remarks>
    /// Named only where there is more than one: <c>lang</c> speaks to a screen reader alone. The
    /// name sits outside the marked span so it is not announced in the language it names, and is a
    /// <c>p</c> so a host with no rule for the class still gets one language per line.
    /// </remarks>
    internal static int Values(RenderTreeBuilder builder, int seq, PropertyRow row, string reader, Texts text)
    {
        foreach (var slot in row.Values)
        {
            builder.OpenElement(seq, "dd");

            if (row.Values.Count == 1)
            {
                builder.AddAttribute(seq + 1, "lang", CatalogueProperties.Foreign(slot.Language, reader));
                Text(builder, seq + 2, slot.Text, row);
            }
            else
            {
                builder.OpenElement(seq + 10, "p");
                builder.AddAttribute(seq + 11, "class", PageLanguage);
                builder.AddContent(seq + 12, text.LanguageName(slot.Language));
                builder.CloseElement();

                builder.OpenElement(seq + 13, "span");
                builder.AddAttribute(seq + 14, "lang", CatalogueProperties.Foreign(slot.Language, reader));
                Text(builder, seq + 15, slot.Text, row);
                builder.CloseElement();
            }

            builder.CloseElement();
            seq += 30;
        }

        return seq;
    }

    /// <summary>
    /// A value, as a link where the catalogue types the property as a URL, as a list of links where
    /// it is several addresses, and as markdown where the row is authored. Consumes twelve sequence
    /// numbers from <paramref name="seq"/>.
    /// </summary>
    /// <remarks>
    /// A field that exists to be followed should be followable (FHIDev/Munin#5385). <c>rel</c>
    /// guards the middle-click and ctrl-click paths; there is no <c>target</c>, so a reader stays
    /// on the page they were reading.
    /// </remarks>
    private static void Text(RenderTreeBuilder builder, int seq, string value, PropertyRow row)
    {
        if (row.Hrefs is { } hrefs)
        {
            builder.OpenElement(seq + 6, "ul");

            foreach (var each in hrefs)
            {
                builder.OpenElement(seq + 7, "li");
                Anchor(builder, seq + 8, each, each);
                builder.CloseElement();
            }

            builder.CloseElement();
            return;
        }

        if (row.Href is not { } href)
        {
            if (row.Authored)
            {
                builder.AddContent(seq + 5, CatalogueMarkdown.Render(value));
            }
            else
            {
                builder.AddContent(seq, value);
            }

            return;
        }

        Anchor(builder, seq + 1, href, value);
    }

    /// <summary>One catalogue link, the same whether it stands alone or in a list. Consumes four.</summary>
    private static void Anchor(RenderTreeBuilder builder, int seq, string href, string label)
    {
        builder.OpenElement(seq, "a");
        builder.AddAttribute(seq + 1, "href", href);
        builder.AddAttribute(seq + 2, "rel", "noopener noreferrer");
        builder.AddContent(seq + 3, label);
        builder.CloseElement();
    }

    /// <summary>Headings at the level of the view's own blocks, so a handed section cannot nest under one.</summary>
    internal static RenderFragment NamedSections(IReadOnlyList<DetailNamedSection>? sections, int level) => builder =>
    {
        foreach (var section in sections ?? [])
        {
            builder.OpenComponent<DetailSection>(0);
            builder.AddComponentParameter(1, nameof(DetailSection.Id), section.Id);
            builder.AddComponentParameter(2, nameof(DetailSection.ChildContent), (RenderFragment)(content =>
            {
                content.AddContent(0, Heading(level, section.Heading, "headline headline-s"));
                content.AddContent(1, section.Body);
            }));
            builder.CloseComponent();
        }
    };

    /// <summary>
    /// One metadata group: its name, then its rows — or, for the catch-all, the complete record.
    /// </summary>
    /// <remarks>
    /// The catch-all is told by its <see cref="PropertyGroup.Key"/> and never by its heading, which
    /// is a curator's to rename. <paramref name="completeRecord"/> null draws every group the plain
    /// way, which is what a surface with no such section wants.
    /// </remarks>
    internal static RenderFragment Group(PropertyGroup group, int level, string? language,
                                         CompleteRecordExtras? completeRecord = null) =>
        Both(Heading(level, group.Name, "headline headline-xxs margin--none munin-explorer-group",
                     language: CatalogueProperties.Foreign(group.NameLanguage, ReaderLanguage.Of(language))),
             GroupBody(group, language, completeRecord));

    /// <summary>
    /// Several groups one after another, for the block a view gathers the unplaced ones under.
    /// </summary>
    /// <remarks>
    /// Both detail views draw that block, and drew it from a private copy of this loop each until
    /// the second copy went one release without the first's changes (Fhi.Metadata-lr6yh).
    /// </remarks>
    internal static RenderFragment Groups(IReadOnlyList<PropertyGroup> groups, int level,
                                          string? language,
                                          CompleteRecordExtras? completeRecord = null) => builder =>
    {
        var seq = 0;

        foreach (var group in groups)
        {
            builder.AddContent(seq++, Group(group, level, language, completeRecord));
        }
    };

    /// <summary>The same group without its heading, for a caller that heads the section itself.</summary>
    /// <remarks>
    /// A group the catalogue placed is a section of the page rather than a block inside one, so its
    /// name heads that section and wears the size the other section headings wear.
    /// Callers appending facts supply the full section field count so a multi-field section keeps
    /// every label, even when the catalogue group itself contains only one row.
    /// </remarks>
    internal static RenderFragment GroupBody(PropertyGroup group, string? language,
                                             CompleteRecordExtras? completeRecord = null,
                                             int? sectionFieldCount = null) => builder =>
    {
        var reader = ReaderLanguage.Of(language);
        var text = Texts.For(language);

        if (completeRecord is { } extras
            && string.Equals(group.Key, CatalogueProperties.CatchAllGroupKey, StringComparison.Ordinal))
        {
            CompleteRecordBody(builder, group, extras, reader, text);
            return;
        }

        builder.OpenElement(0, "dl");
        builder.AddAttribute(1, "class", PageFields);

        Rows(builder, 10, group.Rows, reader, text,
             (sectionFieldCount ?? group.Rows.Count) == 1 ? group.Name : null);

        builder.CloseElement();
    };

    /// <summary>Two fragments as one, so a section can hold a block and a fact list under one heading.</summary>
    internal static RenderFragment Both(RenderFragment first, RenderFragment second) => builder =>
    {
        builder.AddContent(0, first);
        builder.AddContent(1, second);
    };

    /// <summary>
    /// A block of the catalogue's own prose, or nothing where it holds none.
    /// </summary>
    /// <remarks>
    /// Drawn through <see cref="CatalogueMarkdown"/> because these fields are authored with links
    /// and line breaks, and marked with the language the catalogue stores them in.
    /// </remarks>
    internal static RenderFragment Prose(string? text, string cssClass, string? language) => builder =>
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", cssClass);
        builder.AddAttribute(2, "lang", language);
        builder.AddContent(3, CatalogueMarkdown.Render(text));
        builder.CloseElement();
    };

    // The catch-all's own names, which no host stylesheet defines: the package invents them and the
    // sample stylesheets stand in until Fhi.Helsedata.Stiler carries a rule for each.
    private const string CompleteRecordDisclosure = "munin-explorer-complete-record";
    private const string CompleteRecordLead = "munin-explorer-complete-record__lead";
    private const string CompleteRecordFields = "munin-explorer-complete-record__fields";

    /// <summary>
    /// The complete record under its heading: the lead, then a collapsed native
    /// <c>&lt;details&gt;</c> over every field the payload holds plus the facts no property
    /// definition carries. Native for the reason the hierarchy's disclosure is — no JavaScript.
    /// </summary>
    private static void CompleteRecordBody(RenderTreeBuilder builder, PropertyGroup group,
                                           CompleteRecordExtras extras, string reader, Texts text)
    {
        builder.OpenElement(100, "p");
        builder.AddAttribute(101, "class", CompleteRecordLead);
        builder.AddContent(102, extras.Lead);
        builder.CloseElement();

        builder.OpenElement(110, "details");
        builder.AddAttribute(111, "class", CompleteRecordDisclosure);

        builder.OpenElement(112, "summary");
        builder.AddContent(113, text.CompleteRecordSummary);
        builder.CloseElement();

        builder.OpenElement(114, "dl");
        builder.AddAttribute(115, "class", CompleteRecordFields);

        var seq = Rows(builder, 120, group.Rows, reader, text);

        foreach (var (label, value, norwegian) in extras.Facts.Where(f => !string.IsNullOrWhiteSpace(f.Value)))
        {
            builder.OpenElement(seq, "div");

            builder.OpenElement(seq + 1, "dt");
            builder.AddAttribute(seq + 2, "class", "headline headline-xxs margin--none");
            builder.AddContent(seq + 3, label);
            builder.CloseElement();

            builder.OpenElement(seq + 4, "dd");
            builder.AddAttribute(seq + 5, "lang", norwegian ? CatalogueProperties.Foreign("no", reader) : null);
            builder.AddContent(seq + 6, value);
            builder.CloseElement();

            builder.CloseElement();
            seq += 10;
        }

        builder.CloseElement();
        builder.CloseElement();
    }

    /// <summary>
    /// The rows of a definition list, and the sequence number the caller continues from.
    /// </summary>
    /// <remarks>
    /// Shared by the plain group and the complete record so the two cannot drift into two shapes of
    /// row; the list around them is each caller's, since they wear different class names.
    /// </remarks>
    private static int Rows(RenderTreeBuilder builder, int seq, IReadOnlyList<PropertyRow> rows,
                            string reader, Texts text, string? heading = null)
    {
        foreach (var row in rows)
        {
            builder.OpenElement(seq, "div");

            builder.OpenElement(seq + 1, "dt");
            // A lone prose field can already be named by its section. Keep the definition
            // list's term for assistive technology without repeating the visible heading.
            var repeatsHeading = row.Authored && heading is not null &&
                string.Equals(row.Label.Trim(), heading.Trim(), StringComparison.OrdinalIgnoreCase);
            builder.AddAttribute(seq + 2, "class", repeatsHeading
                ? "screenreader-only"
                : "headline headline-xxs margin--none");
            builder.AddAttribute(seq + 3, "lang", CatalogueProperties.Foreign(row.LabelLanguage, reader));
            builder.AddContent(seq + 4, row.Label);
            builder.CloseElement();

            seq = Values(builder, seq + 5, row, reader, text);

            builder.CloseElement();
        }

        return seq;
    }
}
