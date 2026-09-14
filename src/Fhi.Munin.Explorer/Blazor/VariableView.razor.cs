using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A variable in full: the catalogue's metadata about it, what its data looks like, and the blocks
/// saying where it lives.
/// </summary>
/// <remarks>
/// The sibling of <see cref="KildeView"/> and built the same way, for the same reason — a page-shaped
/// view that opens inside the component rather than at a route of its own, because this package has
/// no router and never will.
/// <para>
/// That was an open question on Fhi.Metadata-xbynn until both halves turned out to exist already:
/// <c>SelectedVariableId</c> has always been two-way, so a host mirrors it into its URL exactly as it
/// does search and sorting, and the drill-in pattern was proven by the kilde view. Neither was built
/// for this.
/// </para>
/// </remarks>
public sealed partial class VariableView : ComponentBase
{
    /// <summary>The variable to show. Nothing renders until this is set.</summary>
    [Parameter, EditorRequired]
    public VariableDetail? Variable { get; set; }

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
    /// Sections to place between the metadata and the statistics.
    /// </summary>
    /// <remarks>
    /// The kodeverk section arrives this way rather than being rebuilt here: the panel already draws
    /// it, and one section drawn twice is one section to fix twice.
    /// </remarks>
    [Parameter]
    public RenderFragment? Sections { get; set; }

    /// <inheritdoc cref="KildeView.Trail"/>
    [Parameter]
    public IReadOnlyList<DetailTrailStep>? Trail { get; set; }

    /// <inheritdoc cref="KildeView.Actions"/>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    // Unique per instance so two of these views on one page cannot collide on DOM ids, the same
    // reason VariableSearch carries one. A host mounting a variable beside the one it replaced
    // is the case that makes it real: both views hold the same version ids.
    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];

    private Texts T => Texts.For(Language);

    private string Reader => ReaderLanguage.Of(Language);

    /// <summary>The trail the chassis draws — see <see cref="DetailTrail.Append"/> for the rule.</summary>
    private IReadOnlyList<DetailTrailStep>? PageTrail =>
        Variable is { } variable
            ? DetailTrail.Append(Trail, T.Named(variable.PreferredTerm, variable.Code), Reader)
            : null;

    private int BlockLevel => Math.Min(HeadingLevel + 1, 6);

    private int GroupLevel => Math.Min(HeadingLevel + 2, 6);

    /// <summary>The month this view abbreviates, because the version list's date columns are narrow.</summary>
    /// <remarks>
    /// One width for both places a date is written, and the version list is the one that needs it:
    /// two dates as two columns beside a name and a badge. The data period was narrowed for the
    /// 320px rail it used to sit in and keeps the form (Fhi.Metadata-n39ea).
    /// </remarks>
    private const DateWidth Dates = DateWidth.Narrow;

    /// <summary>
    /// The catalogue's metadata about this variable, grouped as the catalogue groups it.
    /// </summary>
    /// <remarks>
    /// The same call the kilde view makes, unchanged. A variable's groups happen to be Beskrivelse,
    /// Personvern, Skjema, Teknisk and Synlighet where a source's are Datainnsamling, Juridisk and so
    /// on — but nothing here knows either list, which is the point of resolving them from the payload.
    /// </remarks>
    private IReadOnlyList<PropertyGroup> Groups =>
        Variable is { } variable
            ? CatalogueProperties.Groups(variable.PropertyMetadata, variable.AdditionalProperties, Reader,
                                         DrawnElsewhere)
            : [];

    /// <summary>Keys this view renders itself, so the metadata does not repeat them.</summary>
    /// <remarks>
    /// Just the one, and it earns its place: DataType is the only filled-in key in its group on a
    /// typical variable, so dropping it drops the group and leaves the five Runa shows.
    /// </remarks>
    private static readonly HashSet<string> DrawnElsewhere = new(StringComparer.Ordinal) { "DataType" };

    /// <summary>Where the variable lives: which source, under which name.</summary>
    /// <remarks>
    /// The third element says whether the value is the catalogue's own words, the same as the kilde
    /// view. The source's name and short name are stored once, in Norwegian; the kildetype is a
    /// vocabulary this package translates. Marking all three, or none, would be wrong either way.
    /// </remarks>
    private IReadOnlyList<(string Label, string? Value, bool Norwegian)> SourceInformation =>
        Variable is not { } variable
            ? []
            : [
                (T.FieldKildeName, variable.KildeName, true),
                (T.FieldKildeShortName, variable.KildeShortName, true),
                (T.FacetKildeType, T.KildeTypeLabel(variable.KildeType, variable.KildeType), false),
            ];

    /// <summary>The years this variable's data covers, in words, or null when the catalogue has neither end.</summary>
    /// <remarks>
    /// A property rather than a pattern match in the markup, because the contents nav asks the same
    /// question: the block is drawn exactly when there is a period to put in it.
    /// </remarks>
    private string? DataPeriod =>
        Variable is { } variable
            ? CatalogueDate.Period(variable.DataFrom, variable.DataTo, Language, T, Dates)
            : null;

    /// <summary>This variable's data type in the reader's language, or null when the catalogue names none.</summary>
    /// <remarks>
    /// The same shape as <see cref="DataPeriod"/>, and for the same reason: the block is drawn
    /// exactly when there is a label to put in it, and the contents nav asks that same question.
    /// </remarks>
    private string? DataTypeLabel =>
        Variable is { DataType: { } dataType } && !string.IsNullOrWhiteSpace(dataType)
            ? T.DataTypeLabel(dataType)
            : null;

    /// <summary>The heading over the statistics block, which the nav has to name without drawing it.</summary>
    /// <remarks>
    /// Read off <see cref="StatisticsBlock"/> rather than rebuilt here, the same way
    /// <see cref="DatasamlingView.StatisticsHeading"/> is: the block emits this exact string, and
    /// two spellings of it would be two spellings of one fact.
    /// </remarks>
    private string StatisticsHeading => StatisticsBlock.HeadingFor(Variable, T);

    /// <summary>The sections this view draws, in the order it draws them.</summary>
    private IReadOnlyList<DetailTocEntry> Toc { get; set; } = [];

    /// <inheritdoc />
    protected override void OnParametersSet() => Toc = BuildToc();

    /// <summary>This view's own predicates, which are what the nav and the blocks both read.</summary>
    /// <remarks>
    /// The kodeverk section arrives through <see cref="Sections"/> and carries no id of its own, so
    /// the nav does not offer it — this view never learns what the explorer put there.
    /// </remarks>
    private IReadOnlyList<DetailTocEntry> BuildToc()
    {
        if (Variable is not { } variable)
        {
            return [];
        }

        DetailTocBuilder toc = new();

        toc.Add(Groups.Count > 0, DetailSectionIds.Metadata, T.HeadingMetadata);
        toc.Add(Versions.Count > 0, DetailSectionIds.Versions, T.HeadingVersionHistory);
        toc.Add(StatisticsBlock.AnyStatistics(variable), DetailSectionIds.Statistics, StatisticsHeading);
        toc.Add(DetailBlocks.AnyFacts(SourceInformation), DetailSectionIds.Source, T.HeadingSourceInformation);
        toc.Add(DataPeriod is not null, DetailSectionIds.DataPeriod, T.FieldDataPeriod);
        toc.Add(DataTypeLabel is not null, DetailSectionIds.DataType, T.FieldDataType);
        toc.Add(variable.AllVariabelgrupper.Count > 0, DetailSectionIds.VariableGroups, T.FieldVariableGroups);
        toc.Add(variable.AllDatasamlinger.Count > 0, DetailSectionIds.DataCollections, T.HeadingDataCollections);

        return toc.Entries;
    }

    /// <summary>Whether the section with this id is drawn, which is whether the nav names it.</summary>
    private bool Drawn(string id) => Toc.Contains(id);
}
