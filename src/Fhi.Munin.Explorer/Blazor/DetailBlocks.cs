using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The pieces every detail view is built from: a heading at a level the caller picks, a definition
/// list of facts, and one group of the catalogue's own properties.
/// </summary>
/// <remarks>
/// Shared by <see cref="KildeView"/>, <see cref="VariableView"/> and <see cref="DatasamlingView"/>,
/// which drew it from three private copies. Level and class stay the caller's, as they are for
/// <see cref="StatisticsBlock"/>: the same block sits at three different depths.
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

    /// <summary>The class on the one sentence that opens a detail block, so every block's lead reads alike.</summary>
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
                builder.AddContent(seq + 10, value);
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

            // Twenty rather than ten: a row's dd branches run to seq + 12, past where the next row
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
    internal static int Values(RenderTreeBuilder builder, int seq, PropertyRow row, string reader, Texts text,
                               string languageClass)
    {
        foreach (var slot in row.Values)
        {
            builder.OpenElement(seq, "dd");

            if (row.Values.Count == 1)
            {
                builder.AddAttribute(seq + 1, "lang", CatalogueProperties.Foreign(slot.Language, reader));
                Text(builder, seq + 2, slot.Text, row.Href);
            }
            else
            {
                builder.OpenElement(seq + 10, "p");
                builder.AddAttribute(seq + 11, "class", languageClass);
                builder.AddContent(seq + 12, text.LanguageName(slot.Language));
                builder.CloseElement();

                builder.OpenElement(seq + 13, "span");
                builder.AddAttribute(seq + 14, "lang", CatalogueProperties.Foreign(slot.Language, reader));
                Text(builder, seq + 15, slot.Text, row.Href);
                builder.CloseElement();
            }

            builder.CloseElement();
            seq += 30;
        }

        return seq;
    }

    /// <summary>
    /// A value, as a link where the catalogue types the property as a URL. Consumes five sequence
    /// numbers from <paramref name="seq"/>.
    /// </summary>
    /// <remarks>
    /// A field that exists to be followed should be followable (FHIDev/Munin#5385). <c>rel</c>
    /// guards the middle-click and ctrl-click paths; there is no <c>target</c>, so a reader stays
    /// on the page they were reading.
    /// </remarks>
    private static void Text(RenderTreeBuilder builder, int seq, string value, string? href)
    {
        if (href is null)
        {
            builder.AddContent(seq, value);
            return;
        }

        builder.OpenElement(seq + 1, "a");
        builder.AddAttribute(seq + 2, "href", href);
        builder.AddAttribute(seq + 3, "rel", "noopener noreferrer");
        builder.AddContent(seq + 4, value);
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
                                         CompleteRecordExtras? completeRecord = null) => builder =>
    {
        var reader = ReaderLanguage.Of(language);
        var text = Texts.For(language);

        builder.OpenElement(0, $"h{level}");
        builder.AddAttribute(1, "class", "headline headline-xxs margin--none munin-explorer-group");
        builder.AddAttribute(2, "lang", CatalogueProperties.Foreign(group.NameLanguage, reader));
        builder.AddContent(3, group.Name);
        builder.CloseElement();

        if (completeRecord is { } extras
            && string.Equals(group.Key, CatalogueProperties.CatchAllGroupKey, StringComparison.Ordinal))
        {
            CompleteRecordBody(builder, group, extras, reader, text);
            return;
        }

        builder.OpenElement(4, "dl");
        builder.AddAttribute(5, "class", PageFields);

        Rows(builder, 10, group.Rows, reader, text);

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
                            string reader, Texts text)
    {
        foreach (var row in rows)
        {
            builder.OpenElement(seq, "div");

            builder.OpenElement(seq + 1, "dt");
            builder.AddAttribute(seq + 2, "class", "headline headline-xxs margin--none");
            builder.AddAttribute(seq + 3, "lang", CatalogueProperties.Foreign(row.LabelLanguage, reader));
            builder.AddContent(seq + 4, row.Label);
            builder.CloseElement();

            seq = Values(builder, seq + 5, row, reader, text, PageLanguage);

            builder.CloseElement();
        }

        return seq;
    }
}
