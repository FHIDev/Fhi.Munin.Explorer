using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A variable in full: the catalogue's metadata about it, what its data looks like, and a sidebar
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

    /// <inheritdoc cref="VariableExplorer.Language"/>
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

    // Unique per instance so two of these views on one page cannot collide on DOM ids, the same
    // reason VariableExplorer carries one. A host mounting a variable beside the one it replaced
    // is the case that makes it real: both views hold the same version ids.
    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];

    private Texts T => Texts.For(Language);

    private string Reader => ReaderLanguage.Of(Language);

    private int BlockLevel => Math.Min(HeadingLevel + 1, 6);

    private int GroupLevel => Math.Min(HeadingLevel + 2, 6);

    /// <summary>The month this view abbreviates, because both places it writes a date are narrow.</summary>
    /// <remarks>
    /// The sidebar is 320px and "20. september 2022 – 9. november 2022" wraps in it; the version
    /// list writes its two dates as two columns beside a name and a badge. The width is this view's
    /// to pick — the ordinal dot is not, and follows the reader (Fhi.Metadata-n39ea).
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
                                         DrawnInTheSidebar)
            : [];

    /// <summary>Keys this view renders itself, so the metadata does not repeat them.</summary>
    /// <remarks>
    /// Just the one, and it earns its place: DataType is the only filled-in key in its group on a
    /// typical variable, so dropping it drops the group and leaves the five Runa shows.
    /// </remarks>
    private static readonly HashSet<string> DrawnInTheSidebar = new(StringComparer.Ordinal) { "DataType" };

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
}
