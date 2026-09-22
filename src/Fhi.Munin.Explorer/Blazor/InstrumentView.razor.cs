using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// An instrument in full: what it is called, what the catalogue holds about it, how long it has
/// been in use, and the way to the variables collected with it.
/// </summary>
/// <remarks>
/// The sibling of <see cref="VariableView"/> and built the same way — a page-shaped view that opens
/// inside the component rather than at a route of its own, because this package has no router.
/// Munin's own client gives the instrument a route; here it is a drill-in of
/// <see cref="VariableSearch"/>, keyed by <c>?instrumentId=</c> exactly as the whole variable is
/// keyed by <c>?variabelId=</c>.
/// <para>
/// It lists no variables. An instrument can hold thousands, so the Variables section is navigation
/// rather than a table — the same decision <see cref="DatasamlingView"/> made — and the link is
/// this search narrowed to <see cref="VariableFilter.InstrumentIds"/>. How many there are is said
/// in the link and again in the hero row.
/// </para>
/// <para>
/// Ships no CSS, like everything else in this package, and invents no class name: it wears the
/// <see cref="DetailPage"/> chassis alone.
/// </para>
/// </remarks>
public sealed partial class InstrumentView : ComponentBase
{
    /// <summary>The instrument to show. Nothing renders until this is set.</summary>
    [Parameter, EditorRequired]
    public InstrumentDetail? Instrument { get; set; }

    /// <inheritdoc cref="VariableSearch.Language"/>
    [Parameter]
    public string? Language { get; set; }

    /// <inheritdoc cref="KildeView.HeadingLevel"/>
    [Parameter]
    public int HeadingLevel { get; set; } = 2;

    /// <inheritdoc cref="KildeView.HeadingId"/>
    [Parameter]
    public string? HeadingId { get; set; }

    /// <summary>
    /// Where the reader goes for the variables collected with this instrument. Null or blank draws
    /// no Variables section at all.
    /// </summary>
    /// <remarks>
    /// An address rather than a callback, and the section is dropped without one rather than drawn
    /// dead: this package owns no URL, so only the surface above knows where its own search lives.
    /// </remarks>
    [Parameter]
    public string? VariablesHref { get; set; }

    /// <inheritdoc cref="VariableView.NamedSections"/>
    [Parameter]
    public IReadOnlyList<DetailNamedSection>? NamedSections { get; set; }

    /// <inheritdoc cref="VariableView.Sections"/>
    [Parameter]
    public RenderFragment? Sections { get; set; }

    /// <inheritdoc cref="KildeView.Trail"/>
    [Parameter]
    public IReadOnlyList<DetailTrailStep>? Trail { get; set; }

    /// <inheritdoc cref="KildeView.Actions"/>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    private Texts T => Texts.For(Language);

    private string Reader => ReaderLanguage.Of(Language);

    /// <summary>The instrument's name for this reader, and the language it ended up in.</summary>
    private (string? Text, string Language) Named =>
        Instrument is { } instrument ? InstrumentBlock.Name(instrument, Reader) : (null, Reader);

    /// <summary>Its description, resolved the same way.</summary>
    private (string? Text, string Language) Described =>
        Instrument is { } instrument ? InstrumentBlock.Description(instrument, Reader) : (null, Reader);

    private string? NameLang => CatalogueProperties.Foreign(Named.Language, Reader);

    private string? DescriptionLang => CatalogueProperties.Foreign(Described.Language, Reader);

    /// <summary>The code beside the sticky bar's name, on the name block's own terms.</summary>
    /// <remarks>Nothing where the heading has already fallen back to it. (Fhi.Metadata-w13lk)</remarks>
    private string? StickyCode => Named.Text is null ? null : Instrument?.Code;

    /// <summary>The trail the chassis draws — see <see cref="DetailTrail.Append"/> for the rule.</summary>
    private IReadOnlyList<DetailTrailStep>? PageTrail =>
        Instrument is { } instrument
            ? DetailTrail.Append(
                Trail,
                (Named.Text ?? instrument.Code, string.Equals(Named.Language, ReaderLanguage.Norwegian,
                                                              StringComparison.OrdinalIgnoreCase)),
                Reader)
            : null;

    private int BlockLevel => Math.Min(HeadingLevel + 1, 6);

    private int GroupLevel => Math.Min(HeadingLevel + 2, 6);

    /// <summary>The catalogue's own metadata about this instrument, grouped as the catalogue groups it.</summary>
    /// <remarks>
    /// The same call the kilde and variable views make, unchanged: which properties an instrument
    /// has, what they are called and what order they come in all arrive with the payload. The two
    /// keys this view draws itself are left out, so neither is on the page under two labels.
    /// </remarks>
    private IReadOnlyList<PropertyGroup> Groups =>
        Instrument is { } instrument
            ? CatalogueProperties.Groups(instrument.PropertyMetadata, instrument.AdditionalProperties,
                                         Reader, DrawnElsewhere)
            : [];

    /// <summary>Keys this view has already rendered itself, so the metadata does not repeat them.</summary>
    /// <remarks>
    /// Per reader rather than fixed, which is the whole of it: the two English keys are the heading
    /// and the ingress for an English reader and are drawn nowhere else for a Norwegian one, so a
    /// fixed set would withhold the English name from the reader who has only the Norwegian.
    /// </remarks>
    private IReadOnlySet<string> DrawnElsewhere
    {
        get
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);

            if (Drew(Named))
            {
                keys.Add(InstrumentBlock.EnglishNameKey);
            }

            if (Drew(Described))
            {
                keys.Add(InstrumentBlock.EnglishDescriptionKey);
            }

            return keys;
        }
    }

    /// <summary>Whether the name block drew this text out of the English half of the bag.</summary>
    private static bool Drew((string? Text, string Language) resolved) =>
        resolved.Text is not null
        && string.Equals(resolved.Language, ReaderLanguage.English, StringComparison.OrdinalIgnoreCase);

    /// <summary>How long the instrument has been in use, in words, or null when neither end is set.</summary>
    /// <remarks>
    /// A property rather than a pattern match in the markup, because the contents nav asks the same
    /// question: the block is drawn exactly when there is a period to put in it.
    /// </remarks>
    private string? Validity =>
        Instrument is { } instrument
            ? CatalogueDate.Period(instrument.ValidFrom, instrument.ValidTo, Language, T)
            : null;

    /// <summary>Whether the Variables section has both a number to name and somewhere to send the reader.</summary>
    private bool HasVariablesNavigation =>
        Instrument is { VisibleVariableCount: > 0 } && !string.IsNullOrWhiteSpace(VariablesHref);

    /// <summary>The facts an instrument leads with.</summary>
    /// <remarks>
    /// Two rather than the chassis's six: an instrument is a short record, and
    /// <see cref="DetailFacts"/> drops a fact the catalogue holds nothing for rather than drawing
    /// it empty.
    /// </remarks>
    private IReadOnlyList<DetailFact> HeroFacts =>
        Instrument is not { } instrument
            ? []
            : [
                new DetailFact(T.HeadingVariables,
                               instrument.VisibleVariableCount > 0
                                   ? instrument.VisibleVariableCount.ToString()
                                   : null),
                new DetailFact(T.FieldValidity, Validity),
            ];

    /// <summary>The sections this view draws, in the order it draws them.</summary>
    private IReadOnlyList<DetailTocEntry> Toc { get; set; } = [];

    private IReadOnlySet<string> DrawnIds { get; set; } = new HashSet<string>();

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var toc = BuildToc();

        Toc = toc.Entries;
        DrawnIds = toc.Drawn;
    }

    /// <summary>This view's own predicates, which are what the nav and the blocks both read.</summary>
    private DetailTocBuilder BuildToc()
    {
        DetailTocBuilder toc = new();

        if (Instrument is null)
        {
            return toc;
        }

        toc.Add(Groups.Count > 0, DetailSectionIds.Metadata, T.HeadingMetadata);
        toc.Add(Validity is not null, DetailSectionIds.Validity, T.FieldValidity);
        toc.Add(HasVariablesNavigation, DetailSectionIds.Variables, T.HeadingVariables);
        toc.AddNamed(NamedSections);

        return toc;
    }

    /// <summary>Whether this view's own block is drawn; a named section never switches one on.</summary>
    private bool Drawn(string id) => DrawnIds.Contains(id);
}
